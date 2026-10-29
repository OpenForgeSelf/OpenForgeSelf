using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// 031 命令执行工具（2026-10-28 工具归属迁移自 AIAgent → mcp-center，裸名为平台保留名）：
/// 白名单拒绝模式 + 管道/链拒绝 + cwd 必填门三道门。
/// 拒绝路径**不启动任何子进程**；放行经 Process（UseShellExecute=false，不经过 shell 解释）。
/// 安全设计详见 docs/ai/pilot/031-universal-tool-gateway 与 ADR-002 决策 3（独立工具的原因）。
/// 2026-10-29 用户拍板：cwd 必填（绝对路径、目录须存在），彻底移除「未选择项目目录/FORGESELF_DEFAULT_PROJECT_ROOT」
/// 隐式回退与 IProjectWorkspaceService 越界依赖——cwd 即本次调用的工作区锚点（原 CWD 越界门随之退役）。
/// </summary>
/// <remarks>
/// 构造签名保留 IContext 以兼容宿主实例化与既有测试；当前实现不再消费工作区状态。
/// 使用统计经注册表分发自带（post-execute），工具内不重复上报。
/// </remarks>
public class RunTerminalCommandTool : IToolFunctionExtension
{
    // （2026-10-29 移除 private readonly IContext _ctx 字段：cwd 必填化后不再消费工作区服务，见构造函数注释）

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

    public string Id => "mcp-center.run_terminal_command";
    public string Name => "run_terminal_command";
    public string PluginId => "mcp-center";

    public string Description =>
        "执行终端命令（默认白名单拒绝模式）。首 token 必须在白名单（dotnet/pnpm/node/git/ssh/pwsh）。" +
        "支持管道与链式（| && || ; 换行）——每一段都独立过同一套白名单+破坏性红线校验，任一段被拒则整条拒绝；重定向（> <）、反引号、后台单 & 仍拒绝。" +
        "多段命令经 pwsh -NoProfile -NonInteractive -Command 执行，退出码取最后一段。" +
        "入参 {command, cwd（必填，工作目录绝对路径，目录须存在）, timeoutSeconds?, env?}；不再回退当前项目根，未传 cwd 直接报必传错误。" +
        "返回 {exitCode, stdout, stderr}；非 0 退出码是正常回传（失败输出同样是有效观察数据）；超时返回错误（进程后台继续）。" +
        "删除/格式化/下载/提权类命令（rm/Remove-Item/del/format/curl 等）被安全策略拒绝。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""command"": {
            ""type"": ""string"",
            ""description"": ""要执行的完整命令，如 dotnet build / git status / git log | node count.js / dotnet build && dotnet test。支持管道与链式（| && || ; 换行），每段首 token 须在白名单（dotnet/pnpm/node/git/ssh/pwsh）；不得含重定向（> <）、反引号或后台单 &""
        },
        ""cwd"": {
            ""type"": ""string"",
            ""description"": ""工作目录绝对路径（必填，目录须已存在），如 D:\\src\\my-proj\\src；不再回退当前项目根/FORGESELF_DEFAULT_PROJECT_ROOT，未传直接报必传错误""
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
    ""required"": [""command"", ""cwd""]
}";
    public RunTerminalCommandTool(IContext ctx)
    {
        // 2026-10-29 起 cwd 改为必填参数、越界门改为工具内直检，不再消费 IProjectWorkspaceService；
        // 构造签名保留 IContext 以兼容宿主实例化与既有测试（参数当前不使用）。
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
                return Fail("command 为空：入参 {command: 完整命令, cwd: 工作目录绝对路径（必填）, timeoutSeconds?: 1-30}");
            }

            // 2. 第一/二道门 + 破坏性红线（纯逻辑，全部在启动子进程之前；拒绝路径零子进程）
            var guard = TerminalCommandGuard.Check(dto.Command);
            if (!guard.Allowed)
            {
                return Fail(guard.Reason ?? "command rejected");
            }

            // 3. 第三道门：cwd 必填校验（2026-10-29 用户拍板：必须显式传 cwd，彻底移除「未选择项目目录/
            // FORGESELF_DEFAULT_PROJECT_ROOT」隐式回退——cwd 即本次调用的工作区锚点，绝对路径且目录须存在）。
            if (string.IsNullOrWhiteSpace(dto.Cwd))
            {
                return Fail("缺少必填参数 cwd：工作目录绝对路径（如 D:\\src\\my-proj\\src）。不再回退当前项目根/FORGESELF_DEFAULT_PROJECT_ROOT，cwd 必传");
            }
            var cwdRaw = dto.Cwd.Trim();
            if (!Path.IsPathRooted(cwdRaw))
            {
                return Fail("cwd 必须为绝对路径（收到相对路径：" + cwdRaw + "）");
            }
            string cwd = Path.GetFullPath(cwdRaw);
            if (!Directory.Exists(cwd))
            {
                return Fail("cwd 目录不存在：" + cwd);
            }

            // 4. 进程执行（UseShellExecute=false：不经过 shell 解释；PILOT-075 双路径）：
            //    单段命令 = 直接 Process 启动首 token（既有姿势，行为零变化）；
            //    多段命令（| && || ; 换行分隔）= pwsh -NoProfile -NonInteractive -Command 执行完整命令串，
            //    由 ArgumentList 负责转义（不手工拼引号）；退出码取 pwsh 返回值（链式 = 最后一段）。
            TerminalCommandGuard.TrySplitSegments(dto.Command, out var segments, out _);
            var isChain = segments.Count > 1;
            var exeLabel = isChain ? "pwsh(chain)" : TerminalCommandGuard.SplitExecutable(dto.Command).exe;
            var timeoutSeconds = Math.Clamp(
                dto.TimeoutSeconds ?? TerminalCommandGuard.MaxTimeoutSeconds, 1, TerminalCommandGuard.MaxTimeoutSeconds);
            var psi = new ProcessStartInfo
            {
                WorkingDirectory = cwd,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
                // 不设 StandardOutputEncoding/StandardErrorEncoding（PC-011，PILOT-073）：任何固定码页解码
                // 都会毁掉另一侧——固定 UTF-8 毁掉按系统 ANSI（GBK）写出的中文输出，固定跟随码页毁掉
                // dotnet/pnpm/node 的协议性 UTF-8 输出。改为下面捕获原始字节，交 ProcessOutputText 裁决。
            };
            if (isChain)
            {
                psi.FileName = "pwsh";
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-NonInteractive");
                psi.ArgumentList.Add("-Command");
                psi.ArgumentList.Add(dto.Command);
            }
            else
            {
                var (exe, rest) = TerminalCommandGuard.SplitExecutable(dto.Command);
                psi.FileName = exe;
                psi.Arguments = rest;
            }
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
                return Fail($"Error: failed to start process '{exeLabel}'");
            }

            byte[] stdoutBytes;
            byte[] stderrBytes;
            var timedOut = false;
            try
            {
                // 并发读双管道防死锁（stdout 缓冲满会阻塞子进程写 stderr）；读原始字节不做解码。
                var outTask = ProcessOutputText.CaptureAsync(proc.StandardOutput.BaseStream);
                var errTask = ProcessOutputText.CaptureAsync(proc.StandardError.BaseStream);
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
                stdoutBytes = timedOut ? Array.Empty<byte>() : outTask.Result;
                stderrBytes = timedOut ? Array.Empty<byte>() : errTask.Result;
            }
            catch (ObjectDisposedException)
            {
                // 进程句柄已随超时释放，按超时语义处理。
                timedOut = true;
                stdoutBytes = Array.Empty<byte>();
                stderrBytes = Array.Empty<byte>();
            }

            // 解码（UTF-8 严格优先 → 系统 ANSI 往返校验回退 → 兜底宽容 UTF-8）；非 UTF-8 生效时留日志（FR-3）。
            var decodedOut = ProcessOutputText.Decode(stdoutBytes);
            var decodedErr = ProcessOutputText.Decode(stderrBytes);
            if (!decodedOut.EncodingUsed.StartsWith("utf8") || !decodedErr.EncodingUsed.StartsWith("utf8"))
            {
                XTrace.Log.Info("[McpCenter] run_terminal_command 输出按非 UTF-8 还原：stdout={0} stderr={1}",
                    decodedOut.EncodingUsed, decodedErr.EncodingUsed);
            }

            var (stdoutText, stdoutTruncated) = TerminalCommandGuard.Truncate(decodedOut.Text);
            var (stderrText, stderrTruncated) = TerminalCommandGuard.Truncate(decodedErr.Text);
            var exitCode = timedOut ? -1 : SafeExitCode(proc);

            XTrace.Log.Info("[McpCenter] run_terminal_command {0}（{1}ms，timedOut={2}，exit={3}）",
                exeLabel, Environment.TickCount64 - start, timedOut, exitCode);

            if (timedOut)
            {
                // 语义同宿主 ExecuteToolWithTimeoutAsync（提前返回 + 后台继续），stderr 提示沿用 031 design.md §4.3。
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    command = dto.Command,
                    error = $"timeout {timeoutSeconds}s (process continues in background)",
                    timedOut = true,
                    durationMs = Environment.TickCount64 - start
                });
            }

            // 非 0 退出码**非异常**——失败输出同样是有效观察数据（031 design.md §4.3，success=true 正常回传）。
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
            XTrace.Log.Error("[McpCenter] run_terminal_command 执行失败: {0}", ex.Message);
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
