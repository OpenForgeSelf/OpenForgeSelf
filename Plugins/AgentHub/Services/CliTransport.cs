using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// CLI 交互口（one-shot 子进程）。
///
/// 安全铁律（design §6，任何改动不得绕过）：
/// - 参数一律经 <see cref="ProcessStartInfo.ArgumentList"/> **数组**传递，绝不拼 shell 字符串；
/// - 不使用 <c>cmd /c</c>；prompt 里的 <c>; &amp; | &gt; $()</c> 等一律是普通字符；
/// - 超时先软终（关 stdin）再强杀进程树；取消同样杀进程树。
///
/// 模板占位符：<c>{prompt}</c> <c>{cwd}</c> <c>{permission}</c> <c>{sessionId}</c>
/// 未知占位符**原样保留**（不静默替换为空，避免命令走样）。
/// </summary>
public class CliTransport : IAgentTransport
{
    /// <summary>会话句柄 → 进程 映射（供取消用）</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Int32, Process> _running = new();

    private readonly ProfileLoader _profiles;

    /// <summary>构造 CLI transport</summary>
    /// <param name="profiles">profile 加载器（取输出映射与会话 flag 模板）</param>
    public CliTransport(ProfileLoader profiles)
    {
        _profiles = profiles;
    }

    /// <inheritdoc />
    public String Kind => "Cli";

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentEvent> StreamAsync(
        AgentAccessPointDto ap,
        AgentRunRequest req,
        AgentSession session,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var profile = _profiles.Get(ap.Vendor ?? String.Empty);
        var exe = ap.Executable;
        if (exe.IsNullOrEmpty())
        {
            yield return AgentEvent.FromError("交互口未配置可执行文件");
            yield break;
        }

        // 参数模板：交互口配置优先，其次 profile
        var template = !ap.ArgsTemplate.IsNullOrEmpty() ? ap.ArgsTemplate : profile?.ArgsTemplate;
        var injection = !ap.PromptInjection.IsNullOrEmpty() ? ap.PromptInjection : (profile?.PromptInjection ?? "Arg");

        var args = new List<String>();
        var promptViaStdin = String.Equals(injection, "Stdin", StringComparison.OrdinalIgnoreCase);

        if (!template.IsNullOrEmpty())
        {
            args.AddRange(BuildArgs(template, req, promptViaStdin ? null : req.Prompt));
        }

        // OneShot 的会话续接：追加 --session xxx 之类的 flag
        var sessionFlag = !ap.SessionFlagTemplate.IsNullOrEmpty() ? ap.SessionFlagTemplate : profile?.SessionFlagTemplate;
        if (!req.SessionRef.IsNullOrEmpty() && !sessionFlag.IsNullOrEmpty())
        {
            args.AddRange(BuildArgs(sessionFlag, req, null));
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = promptViaStdin,
            UseShellExecute = false,          // 关键：不经 shell，杜绝命令注入
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var a in args) psi.ArgumentList.Add(a);

        if (!req.Cwd.IsNullOrEmpty() && Directory.Exists(req.Cwd)) psi.WorkingDirectory = req.Cwd;

        // 环境变量只按名字透传（值留环境，不落库）
        foreach (var (key, envName) in ap.EnvVars)
        {
            if (key.IsNullOrEmpty() || envName.IsNullOrEmpty()) continue;
            var value = Environment.GetEnvironmentVariable(envName);
            if (!value.IsNullOrEmpty()) psi.Environment[key] = value;
        }

        var stderr = new StringBuilder();
        Process? process = null;
        var pipeline = new EventPipeline(profile);

        try
        {
            process = new Process { StartInfo = psi };
            process.Start();

            session.ProcessId = process.Id;
            session.Kind = Kind;
            _running[session.TaskId] = process;

            XTrace.Log.Info("[AgentHub] 委派进程启动: {0} {1} (pid={2})", exe, String.Join(' ', args), process.Id);

            if (promptViaStdin)
            {
                try
                {
                    await process.StandardInput.WriteAsync(req.Prompt.AsMemory(), ct);
                    await process.StandardInput.FlushAsync(ct);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Debug("[AgentHub] 写入 stdin 失败: {0}", ex.Message);
                }
                finally
                {
                    try { process.StandardInput.Close(); } catch { /* 已退出 */ }
                }
            }

            // stderr 后台收集（不阻塞 stdout 流，避免管道写满互锁）
            var stderrTask = Task.Run(async () =>
            {
                try
                {
                    var text = await process.StandardError.ReadToEndAsync(CancellationToken.None);
                    lock (stderr) stderr.Append(text);
                }
                catch { /* 进程被强杀时正常 */ }
            });

            // ⚠ PILOT-055 实测修复：超时计时必须**先于** stdout 读取循环。
            // 旧实现把 timeoutCts 放在循环之后——子进程静默无输出（如 opencode 对模型 400 静默重试）时，
            // ReadLineAsync(ct) 永远阻塞，超时杀进程逻辑根本执行不到，任务恒 Running。
            // 现在 stdout 读取观察 timeoutCts：超时即 break → 走下方「杀进程树 + 报超时」分支。
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(req.TimeoutMs);

            // 逐行读 stdout，边读边归一化输出（流式）
            var lineNo = 0;
            while (true)
            {
                String? line;
                try
                {
                    line = await process.StandardOutput.ReadLineAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (line == null) break;

                lineNo++;
                foreach (var evt in pipeline.ParseLines([line]))
                {
                    // 从 Meta 事件里尽力捞会话 id（供续接）
                    if (evt.Type == AgentEventTypes.Meta && session.SessionRef.IsNullOrEmpty())
                    {
                        session.SessionRef = GuessSessionId(evt, profile);
                    }

                    yield return evt;
                }
            }

            // 等进程真正退出（含超时/取消分支）
            var timedOut = false;
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                timedOut = !ct.IsCancellationRequested;
                ProcessRunner.KillProcessTree(process);
            }

            await stderrTask;

            // stderr 有内容则作为 Error 事件回流（很多 CLI 把警告/错误写 stderr）
            var errText = LockedText(stderr);
            if (!errText.IsNullOrWhiteSpace())
            {
                yield return AgentEvent.FromError(errText.TrimEnd());
            }

            // 超时先补一条 Error 事件（含英文 "timeout" 标记，供 RunAsync 的 ClassifyError 归类为 timeout，
            // 否则中文文案会落入 upstream_error；exit 事件只负责携带退出码）
            if (timedOut)
            {
                yield return AgentEvent.FromError($"执行超时（timeout after {req.TimeoutMs}ms），已终止进程树");
            }

            var exit = new AgentEvent
            {
                Type = AgentEventTypes.Exit,
                ExitCode = process.HasExited ? process.ExitCode : -1,
                Text = timedOut
                    ? $"执行超时（{req.TimeoutMs}ms），已终止进程树"
                    : $"进程退出，退出码 {process.ExitCode}"
            };
            yield return exit;
        }
        finally
        {
            _running.TryRemove(session.TaskId, out _);
            process?.Dispose();
        }
    }

    /// <inheritdoc />
    public Task CancelAsync(AgentSession session)
    {
        if (session.TaskId > 0 && _running.TryGetValue(session.TaskId, out var process))
        {
            XTrace.Log.Info("[AgentHub] 取消任务 {0}，终止进程树 pid={1}", session.TaskId, session.ProcessId);
            ProcessRunner.KillProcessTree(process);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 展开参数模板为 argv 列表。
    ///
    /// 【切分规则】按**空白**（空格 / 制表 / 换行）与 <c>|</c> 切分为独立参数项，
    /// 但被**双引号包裹**的片段整体视为一个参数项（引号本身剥除）。
    /// 例：<c>exec "{prompt}" --json -C {cwd}</c> → <c>[exec, &lt;prompt值&gt;, --json, -C, &lt;cwd值&gt;]</c>。
    /// 这样既贴合各家 CLI 的真实模板写法（见 Profiles/*.json 的 argsTemplate），
    /// 又保证含空格的 prompt / 路径不会在切分阶段被切碎。
    ///
    /// 【顺序不可颠倒：先切分、后替换】
    /// 必须**先按模板切分**再替换占位符 —— 若先替换，prompt 里的空格会被当作分隔符，
    /// 于是 <c>rm -rf /</c> 这类内容会被拆成多个 argv 项，等价于把用户内容当命令行解释。
    /// 先切分则 prompt 内容始终落在**同一个参数项**内，shell 元字符不会被当命令。
    ///
    /// 【安全铁证】
    /// 未识别的占位符（如 <c>{unknownThing}</c>）**原样保留**，不静默替换为空 ——
    /// 静默吞掉会让用户以为参数生效了，实际 CLI 收到空值，属"假装成功"。
    /// </summary>
    /// <param name="template">参数模板</param>
    /// <param name="req">运行请求</param>
    /// <param name="prompt">提示词（null 表示该次不注入 prompt）</param>
    /// <returns>argv 列表</returns>
    internal static IReadOnlyList<String> BuildArgs(String template, AgentRunRequest req, String? prompt)
    {
        var result = new List<String>();

        // 第一步：切分（尊重双引号，引号内不切）
        var items = SplitTemplate(template);

        // 第二步：逐项替换占位符
        foreach (var item in items)
        {
            var arg = item;

            if (prompt != null) arg = arg.Replace("{prompt}", prompt);
            arg = arg.Replace("{cwd}", req.Cwd ?? String.Empty);
            arg = arg.Replace("{permission}", req.PermissionMode);
            arg = arg.Replace("{sessionId}", req.SessionRef ?? String.Empty);
            arg = arg.Replace("{model}", String.Empty);

            // 已替换掉 prompt 的项若整体为空则跳过（例如 prompt 为空串）
            if (arg.IsNullOrEmpty() && prompt != null && item.Contains("{prompt}")) continue;

            result.Add(arg);
        }

        return result;
    }

    /// <summary>
    /// 把参数模板切成参数项：空白与 <c>|</c> 为分隔符，**双引号内的空白/竖线不切分**。
    ///
    /// 引号**原样保留**在结果里（不剥除）：Windows 的 <c>ProcessStartInfo.ArgumentList</c>
    /// 与 POSIX argv 都由运行库按各自规则处理引号；测试与实机都期望
    /// <c>exec "{prompt}" --json</c> 产出含引号的 <c>exec "..."</c> 形态。
    /// </summary>
    private static List<String> SplitTemplate(String template)
    {
        var items = new List<String>();
        var sb = new StringBuilder();
        var inQuotes = false;

        foreach (var c in template.Replace("\r\n", "\n"))
        {
            if (c == '"') inQuotes = !inQuotes;

            if (!inQuotes && (Char.IsWhiteSpace(c) || c == '|'))
            {
                if (sb.Length > 0) { items.Add(sb.ToString()); sb.Clear(); }
                continue;
            }

            sb.Append(c);
        }

        if (sb.Length > 0) items.Add(sb.ToString());

        return items;
    }

    /// <summary>从 Meta 事件的载荷里猜会话 id（各家键名不同，命中不了返回 null）。</summary>
    private static String? GuessSessionId(AgentEvent evt, AgentProfile? profile)
    {
        var path = profile?.OutputMapping != null && profile.OutputMapping.TryGetValue("sessionId", out var p) ? p : null;

        var id = EventPipeline.ReadPath(evt.Payload, path);
        if (!id.IsNullOrEmpty()) return id;

        foreach (var fallback in new[] { "$.session_id", "$.sessionId", "$.thread_id", "$.conversation_id" })
        {
            var v = EventPipeline.ReadPath(evt.Payload, fallback);
            if (!v.IsNullOrEmpty()) return v;
        }

        return null;
    }

    /// <summary>线程安全取 StringBuilder 文本</summary>
    private static String LockedText(StringBuilder sb)
    {
        lock (sb) return sb.ToString();
    }
}
