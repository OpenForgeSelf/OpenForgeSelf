using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent;

/// <summary>
/// 031 命令执行工具：白名单拒绝模式 + 管道/链拒绝 + CWD 越界门三道门。
/// 拒绝路径**不启动任何子进程**；放行经 Process（UseShellExecute=false，不经过 shell 解释）。
/// 安全设计详见 specs/031-universal-tool-gateway/design.md §4 与 ADR-002 决策 3（独立工具的原因）。
/// </summary>
/// <remarks>
/// 与 UniversalTool 同范式：构造期注入 IContext，运行时软依赖 ctx.Get&lt;T&gt;()（宿主 seed 晚于插件 Apply）。
/// 使用统计经注册表分发自带（post-execute），工具内不重复上报（与 UniversalTool 范式一致）。
/// </remarks>
public class RunTerminalCommandTool : IToolFunctionExtension
{
    private readonly IContext _ctx;

    /// <summary>
    /// 大小写不敏感反序列化。模型按 ParametersJsonSchema 发 camelCase（command），
    /// 而 TerminalCommandDto 属性是 PascalCase（Command），System.Text.Json 默认敏感会**静默丢参**
    /// （2026-09-15 实测：所有调用都报"command 为空"，根因即此）。
    /// 修复层级选工具侧而非给契约 DTO 加 JsonPropertyName：模型可见契约只有 JSON Schema，DTO 命名是实现细节。
    /// </summary>
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Id => "aiagent.run_terminal_command";
    public string Name => "run_terminal_command";
    public string PluginId { get; }

    public string Description =>
        "执行终端命令（默认白名单拒绝模式）。首 token 必须在白名单（dotnet/pnpm/node/git/ssh/pwsh），" +
        "管道与链式（| ; && > < 换行）拒绝，CWD 必须在当前登记项目根内。入参 {command, cwd?, timeoutSeconds?}。" +
        "返回 {exitCode, stdout, stderr}；非 0 退出码是正常回传（失败输出同样是有效观察数据）；超时返回错误（进程后台继续）。" +
        "删除/格式化/下载/提权类命令（rm/Remove-Item/del/format/curl 等）被安全策略拒绝。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""command"": {
            ""type"": ""string"",
            ""description"": ""要执行的完整命令，如 dotnet build / git status / pnpm --version。首 token 须在白名单（dotnet/pnpm/node/git/ssh/pwsh）；不得含管道与链式分隔符""
        },
        ""cwd"": {
            ""type"": ""string"",
            ""description"": ""工作目录（当前登记项目根内的相对路径，空=项目根；越界被拒）""
        },
        ""timeoutSeconds"": {
            ""type"": ""integer"",
            ""description"": ""超时秒数（1-30，默认30；超时后进程在后台继续，结果标记 timedOut）""
        },
        ""env"": {
            ""type"": ""object"",
            ""description"": ""附加环境变量（键值对，可选）"",
            ""additionalProperties"": { ""type"": ""string"" }
        }
    },
    ""required"": [""command""]
}";

    public RunTerminalCommandTool(string pluginId, IContext ctx)
    {
        PluginId = pluginId;
        _ctx = ctx;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var start = Environment.TickCount64;
        try
        {
            // 1. 入参解析（大小写不敏感——模型侧契约是 camelCase schema，DTO 是 PascalCase 属性）
            TerminalCommandDto dto;
            try
            {
                dto = JsonSerializer.Deserialize<TerminalCommandDto>(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters, CaseInsensitiveOptions)
                    ?? new TerminalCommandDto();
            }
            catch (JsonException jex)
            {
                return Fail($"入参必须是 JSON 对象（{nameof(TerminalCommandDto)}）：{jex.Message}");
            }

            if (string.IsNullOrWhiteSpace(dto.Command))
            {
                return Fail("command 为空：入参 {command: 完整命令, cwd?: 项目根内相对路径, timeoutSeconds?: 1-30}");
            }

            // 2. 第一/二道门 + 破坏性红线（纯逻辑，全部在启动子进程之前；拒绝路径零子进程）
            var guard = TerminalCommandGuard.Check(dto.Command);
            if (!guard.Allowed)
            {
                return Fail(guard.Reason ?? "command rejected");
            }

            // 3. 第三道门：CWD 越界检查（复用 ResolveSafePath 的越界防护；未登记项目 → 引导文案）
            var workspace = _ctx.Get<IProjectWorkspaceService>();
            if (workspace == null)
            {
                return Fail("项目工作区服务不可用：请先在左侧选择工作目录再执行命令");
            }
            string cwd;
            try
            {
                cwd = workspace.ResolveSafePath(dto.Cwd?.Replace('\\', '/') ?? string.Empty);
            }
            catch (InvalidOperationException iex) when (iex.Message.Contains("未选择"))
            {
                // ResolveSafePath 的"未选项目"文案已含引导（先在左侧选择工作目录）。
                return Fail(iex.Message);
            }
            catch (InvalidOperationException iex)
            {
                XTrace.Log.Warn("[AIAgentPlugin] run_terminal_command CWD 越界：{0}", iex.Message);
                return Fail($"Error: cwd escapes registered project root（{iex.Message}）");
            }

            // 4. 进程执行（UseShellExecute=false：不经过 shell 解释；管道/链已在守卫层拒绝）
            var (exe, rest) = TerminalCommandGuard.SplitExecutable(dto.Command);
            var timeoutSeconds = Math.Clamp(
                dto.TimeoutSeconds ?? TerminalCommandGuard.MaxTimeoutSeconds, 1, TerminalCommandGuard.MaxTimeoutSeconds);
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = rest,
                WorkingDirectory = cwd,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };
            if (dto.Env != null)
            {
                foreach (var kv in dto.Env)
                {
                    psi.EnvironmentVariables[kv.Key] = kv.Value;
                }
            }

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return Fail($"Error: failed to start process '{exe}'");
            }

            string stdout;
            string stderr;
            var timedOut = false;
            try
            {
                // 并发读双管道防死锁（stdout 缓冲满会阻塞子进程写 stderr）。
                var outTask = proc.StandardOutput.ReadToEndAsync();
                var errTask = proc.StandardError.ReadToEndAsync();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                try
                {
                    await proc.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    timedOut = true;
                }
                if (!timedOut)
                {
                    await Task.WhenAll(outTask, errTask);
                }
                stdout = timedOut ? string.Empty : outTask.Result;
                stderr = timedOut ? string.Empty : errTask.Result;
            }
            catch (ObjectDisposedException)
            {
                // 进程句柄已随超时释放，按超时语义处理。
                timedOut = true;
                stdout = string.Empty;
                stderr = string.Empty;
            }

            var (stdoutText, stdoutTruncated) = TerminalCommandGuard.Truncate(stdout);
            var (stderrText, stderrTruncated) = TerminalCommandGuard.Truncate(stderr);
            var exitCode = timedOut ? -1 : SafeExitCode(proc);

            XTrace.Log.Info("[AIAgentPlugin] run_terminal_command {0}（{1}ms，timedOut={2}，exit={3}）",
                exe, Environment.TickCount64 - start, timedOut, exitCode);

            if (timedOut)
            {
                // 语义同宿主 ExecuteToolWithTimeoutAsync（提前返回 + 后台继续），stderr 提示沿用 design.md §4.3。
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    command = dto.Command,
                    error = $"timeout {timeoutSeconds}s (process continues in background)",
                    timedOut = true,
                    durationMs = Environment.TickCount64 - start
                });
            }

            // 非 0 退出码**非异常**——失败输出同样是有效观察数据（design.md §4.3，success=true 正常回传）。
            return JsonSerializer.Serialize(new
            {
                success = true,
                command = dto.Command,
                cwd = cwd,
                exitCode = exitCode,
                stdout = stdoutText,
                stderr = stderrText,
                stdoutTruncated = stdoutTruncated,
                stderrTruncated = stderrTruncated,
                durationMs = Environment.TickCount64 - start
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] run_terminal_command 执行失败: {0}", ex.Message);
            return Fail(ex.Message);
        }
    }

    private static int SafeExitCode(Process proc)
    {
        try { return proc.ExitCode; }
        catch { return -1; }
    }

    private static string Fail(string error)
    {
        return JsonSerializer.Serialize(new { success = false, error });
    }
}
