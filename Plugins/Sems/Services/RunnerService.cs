// CA1416: WMI（System.Management）只在 Windows 分支调用。非 Windows 读 /proc，不调用 WMI。
// sems 插件 TargetFramework=net10.0，WMI API 会触发「仅 windows 支持」警告，
// 故在本文件作用域禁用 CA1416（不在 sems.csproj 层禁用，避免掩盖其他真问题）。
#pragma warning disable CA1416
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Text;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.Sems.Services;

/// <summary>
/// 运行会话（Launched 面板启动 / Detected 本机进程检测捕获）。
/// </summary>
public class RunSession
{
    /// <summary>命令 Id（Detected 条目无命令时取 0）。</summary>
    public int CommandId { get; set; }

    /// <summary>所属项目 Id。</summary>
    public int ProjectId { get; set; }

    /// <summary>项目名。</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>命令名（Detected 条目为可执行文件名）。</summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>进程 PID。</summary>
    public int Pid { get; set; }

    /// <summary>启动时间。</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>来源：Launched（本面板启动） / Detected（本机进程检测捕获）。</summary>
    public string Origin { get; set; } = "Launched";
}

/// <summary>
/// 停止操作的可区分结果（PILOT-087，输入1 2026-11-23）：停止反馈必须能区分
/// 「已停掉 / 本来就没在运行 / 真失败」，不允许把三者折叠成一个布尔。
/// </summary>
public enum StopOutcome
{
    /// <summary>进程树已被本次调用终止。</summary>
    Stopped,

    /// <summary>目标本就不在运行（会话不存在 / 进程已自行退出 / 宿主或插件重启过）：不算失败。</summary>
    AlreadyStopped,

    /// <summary>进程仍在运行但无法终止（权限不足等）：界面须提示原因并保留重试入口。</summary>
    Failed
}

/// <summary>一次停止调用的结果（<see cref="IRunnerService.Stop"/> / <see cref="IRunnerService.StopExternal"/>）。</summary>
public sealed class StopResult
{
    /// <summary>结果类别。</summary>
    public StopOutcome Outcome { get; init; }

    /// <summary>目标 PID（按 commandId 停止但会话不存在时为 0）。</summary>
    public int Pid { get; init; }

    /// <summary>可展示的原因：AlreadyStopped 说明为何不在运行，Failed 说明失败原因。</summary>
    public string? Reason { get; init; }

    public static StopResult Stopped(int pid) => new() { Outcome = StopOutcome.Stopped, Pid = pid };

    public static StopResult AlreadyStopped(int pid, string reason) =>
        new() { Outcome = StopOutcome.AlreadyStopped, Pid = pid, Reason = reason };

    public static StopResult Failed(int pid, string reason) =>
        new() { Outcome = StopOutcome.Failed, Pid = pid, Reason = reason };
}

/// <summary>
/// sems 进程运行管理（design §5 / §1.2）：进程启动 / 停止 / 本机进程检测合并。
/// 插件内服务，不进 Abstractions。经 <c>IContext.Get&lt;IProjectRegistry&gt;()</c> 读取命令与项目根，
/// 每次使用每次解析（不缓存接缝实例为字段，符合宿主契约消费铁律）。
/// 会话表内存维护，宿主重启清空（NFR-3）。
/// </summary>
public interface IRunnerService
{
    /// <summary>最近一次 <see cref="Launch"/> 失败的原因。成功或尚未调用时为 null。</summary>
    string? LastLaunchError { get; }

    /// <summary>
    /// 在项目根目录执行已登记脚本。Windows 用 cmd.exe /c，其他系统用 /bin/sh -c。
    /// 同 commandId 已存活、命令不存在、脚本为空、目录不可达或启动异常时返回 null，原因见 <see cref="LastLaunchError"/>。
    /// </summary>
    RunSession? Launch(int commandId);

    /// <summary>停止「本面板启动」的会话（按 commandId 取 PID 杀整树）。结果可区分：已停 / 本就不在运行 / 真失败（PILOT-087）。</summary>
    StopResult Stop(int commandId);

    /// <summary>停止外部捕获进程（按 PID 杀整树）。结果可区分：已停 / 本就不在运行 / 真失败（PILOT-087）。</summary>
    StopResult StopExternal(int pid);

    /// <summary>本机进程检测（Windows：WMI；其他：/proc）+ 与 Launched 合并，返回当前运行列表。</summary>
    IReadOnlyList<RunSession> CheckAndMerge();

    /// <summary>当前运行列表（Launched + Detected）。</summary>
    IReadOnlyList<RunSession> Current();

    /// <summary>停止全部本面板启动的会话（清理/测试用）。</summary>
    void StopAll();

    /// <summary>Launched 会话的输出日志（增量）；无缓冲或未由本面板启动过则 null。</summary>
    RunLogSnapshot? GetLogs(int commandId, int afterIndex);
}

/// <summary>一条捕获的输出（stdout / stderr）。</summary>
public sealed class RunLogLine
{
    public int Index { get; init; }
    public string Stream { get; init; } = "stdout";
    public string Text { get; init; } = string.Empty;
    public DateTime At { get; init; }
}

/// <summary>GET logs 响应片段。</summary>
public sealed class RunLogSnapshot
{
    public int CommandId { get; init; }
    public bool Running { get; init; }
    public IReadOnlyList<RunLogLine> Lines { get; init; } = Array.Empty<RunLogLine>();
    public int NextIndex { get; init; }
    public bool Truncated { get; init; }
}

/// <summary>单命令输出环形缓冲（宿主内存，重启清空）。</summary>
internal sealed class RunLogBuffer
{
    private const int MaxLines = 4000;
    private readonly List<RunLogLine> _lines = new();
    private readonly object _gate = new();
    private int _nextIndex;
    public bool Truncated { get; private set; }

    public void Append(string stream, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        lock (_gate)
        {
            if (_lines.Count >= MaxLines)
            {
                Truncated = true;
                return;
            }

            _lines.Add(new RunLogLine
            {
                Index = _nextIndex++,
                Stream = stream,
                Text = text,
                At = DateTime.Now
            });
        }
    }

    public RunLogSnapshot Snapshot(int commandId, bool running, int afterIndex)
    {
        lock (_gate)
        {
            var slice = _lines.Where(l => l.Index >= afterIndex).ToList();
            return new RunLogSnapshot
            {
                CommandId = commandId,
                Running = running,
                Lines = slice,
                NextIndex = _nextIndex,
                Truncated = Truncated
            };
        }
    }
}

public class RunnerService : IRunnerService
{
    private readonly IContext _ctx;
    // Launched 会话：commandId → 会话。进程 Exited 时移除。
    private readonly ConcurrentDictionary<int, RunSession> _launched = new();
    // 上次 CheckAndMerge 的 Detected 会话：pid → 会话。Current() 只返回仍存活且未被 Launched 覆盖的。
    private readonly ConcurrentDictionary<int, RunSession> _detected = new();
    private readonly ConcurrentDictionary<int, RunLogBuffer> _logs = new();
    private readonly ConcurrentDictionary<int, Process> _processes = new();

    /// <inheritdoc />
    public string? LastLaunchError { get; private set; }

    public RunnerService(IContext ctx)
    {
        _ctx = ctx;
    }

    private IProjectRegistry? Registry => _ctx.Get<IProjectRegistry>();

    /// <summary>
    /// 在命令所属项目根目录启动进程。失败时返回 null，并把可展示的原因写入 <see cref="LastLaunchError"/>。
    /// </summary>
    public RunSession? Launch(int commandId)
    {
        LastLaunchError = null;

        if (_launched.TryGetValue(commandId, out var existing))
        {
            if (IsProcessAlive(existing.Pid))
            {
                LastLaunchError = "该命令已有存活会话，请先停止再启动";
                return null;
            }

            // 进程已退出但 Exited 尚未摘掉会话时，允许重新启动。
            _launched.TryRemove(commandId, out _);
            if (_processes.TryRemove(commandId, out var stale))
            {
                try { stale.Dispose(); } catch { /* 已退出 */ }
            }
        }

        var (project, command) = FindCommand(commandId);
        if (project == null || command == null)
        {
            LastLaunchError = "命令不存在";
            return null;
        }

        var script = command.Script;
        if (string.IsNullOrWhiteSpace(script))
        {
            LastLaunchError = "命令脚本为空，不能启动";
            return null;
        }

        var root = project.Root;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            LastLaunchError = $"项目目录不可达：{root}。请恢复该目录或编辑档案后再启动。";
            return null;
        }

        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", "/c " + script)
            : new ProcessStartInfo("/bin/sh");
        if (!OperatingSystem.IsWindows())
        {
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(script);
        }

        psi.WorkingDirectory = root;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        Process process;
        try
        {
            process = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start 返回 null");
        }
        catch (Exception ex)
        {
            LastLaunchError = "启动失败：" + ex.Message;
            return null;
        }

        var session = new RunSession
        {
            CommandId = command.Id,
            ProjectId = project.Id,
            ProjectName = project.Name,
            CommandName = command.Name,
            Pid = process.Id,
            StartedAt = DateTime.Now,
            Origin = "Launched"
        };

        var logBuf = new RunLogBuffer();
        _logs[commandId] = logBuf;
        _processes[commandId] = process;
        logBuf.Append("stdout", $"[{DateTime.Now:HH:mm:ss}] 已在 {root} 启动：{script}");

        _ = PumpOutputAsync(process.StandardOutput.BaseStream, "stdout", logBuf, process);
        _ = PumpOutputAsync(process.StandardError.BaseStream, "stderr", logBuf, process);

        // 进程退出时从会话表移除（日志缓冲保留至下次 Launch 覆盖）
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            _launched.TryRemove(commandId, out _);
            _processes.TryRemove(commandId, out _);
            logBuf.Append("stdout", $"[{DateTime.Now:HH:mm:ss}] 进程已退出（PID {process.Id}）");
            try { process.Dispose(); } catch { }
        };

        _launched[commandId] = session;
        return session;
    }

    public RunLogSnapshot? GetLogs(int commandId, int afterIndex)
    {
        if (!_logs.TryGetValue(commandId, out var buf)) return null;
        var running = _launched.ContainsKey(commandId) && IsProcessAlive(_launched[commandId].Pid);
        return buf.Snapshot(commandId, running, Math.Max(0, afterIndex));
    }

    /// <summary>
    /// 停止本面板启动的会话：按 commandId 取 PID，杀整树。
    /// PILOT-087：会话不存在（进程已自行退出 / 宿主或插件重启过）返回 <see cref="StopOutcome.AlreadyStopped"/>
    /// （不算失败，界面据此收敛状态）；进程仍在但杀不掉才返回 <see cref="StopOutcome.Failed"/>（带原因）。
    /// </summary>
    public StopResult Stop(int commandId)
    {
        if (!_launched.TryGetValue(commandId, out var session))
            return StopResult.AlreadyStopped(0, $"该命令没有存活的会话（{commandId}）：进程可能已自行退出，或宿主/插件重启过");

        var (killed, error) = KillTree(session.Pid);
        if (!killed && IsProcessAlive(session.Pid))
            return StopResult.Failed(session.Pid, $"进程仍在运行但无法终止：PID {session.Pid}（{error ?? "权限不足或被系统保护"}）");

        _launched.TryRemove(commandId, out _);
        if (_processes.TryRemove(commandId, out var proc))
        {
            try { proc.Dispose(); } catch { /* 已退出 */ }
        }

        return killed
            ? StopResult.Stopped(session.Pid)
            : StopResult.AlreadyStopped(session.Pid, $"进程已自行退出（PID {session.Pid}），无需停止");
    }

    /// <summary>停止外部捕获进程（按 PID 杀整树）。结果可区分：已停 / 已不在运行（不算失败）/ 真失败（带原因）。</summary>
    public StopResult StopExternal(int pid)
    {
        if (pid <= 0) return StopResult.Failed(pid, $"非法 PID：{pid}");
        if (!IsProcessAlive(pid))
        {
            _detected.TryRemove(pid, out _);
            return StopResult.AlreadyStopped(pid, $"进程已不在运行（PID {pid}）：可能已自行退出或宿主重启过");
        }

        var (killed, error) = KillTree(pid);
        if (killed)
        {
            _detected.TryRemove(pid, out _);
            return StopResult.Stopped(pid);
        }
        if (!IsProcessAlive(pid))
        {
            _detected.TryRemove(pid, out _);
            return StopResult.AlreadyStopped(pid, $"进程已不在运行（PID {pid}）");
        }

        return StopResult.Failed(pid, $"进程仍在运行但无法终止：PID {pid}（{error ?? "权限不足或被系统保护"}）");
    }

    /// <summary>本机进程检测 + 与 Launched 合并，并刷新 Detected 缓存供 <see cref="Current"/> 使用。</summary>
    public IReadOnlyList<RunSession> CheckAndMerge()
    {
        var detected = Detect();
        var launched = AliveLaunched();
        var merged = MergeDetected(launched, detected, replaceCache: true);
        return merged;
    }

    /// <summary>当前运行列表：仍在运行的本页启动会话，加上上次检查缓存里还活着的检测会话。</summary>
    public IReadOnlyList<RunSession> Current()
    {
        var launched = AliveLaunched();
        return MergeDetected(launched, _detected.Values.ToList(), replaceCache: true);
    }

    /// <summary>停止全部本面板启动的会话。</summary>
    public void StopAll()
    {
        foreach (var kvp in _launched.ToArray())
        {
            try { KillTree(kvp.Value.Pid); } catch { }
        }
    }

    #region 内部辅助

    private static async Task PumpOutputAsync(Stream stream, string streamName, RunLogBuffer buffer, Process proc)
    {
        var lineBuf = new List<byte>(256);
        var chunk = new byte[4096];
        try
        {
            while (true)
            {
                var n = await stream.ReadAsync(chunk).ConfigureAwait(false);
                if (n <= 0) break;
                for (var i = 0; i < n; i++)
                {
                    var b = chunk[i];
                    if (b == (byte)'\r') continue;
                    if (b == (byte)'\n')
                    {
                        FlushLine(lineBuf, streamName, buffer);
                        continue;
                    }

                    lineBuf.Add(b);
                    if (lineBuf.Count >= 16_384)
                    {
                        FlushLine(lineBuf, streamName, buffer);
                    }
                }
            }

            FlushLine(lineBuf, streamName, buffer);
        }
        catch
        {
            // 管道关闭或进程被杀：停止抽干即可
        }
    }

    private static void FlushLine(List<byte> lineBuf, string streamName, RunLogBuffer buffer)
    {
        if (lineBuf.Count == 0) return;
        var decoded = ProcessOutputText.Decode(lineBuf.ToArray());
        buffer.Append(streamName, decoded.Text.TrimEnd('\r', '\n'));
        lineBuf.Clear();
    }

    /// <summary>
    /// 经接缝查找命令及其所属项目（commandId 全局唯一）。
    /// 返回 (ProjectInfo?, RunCommandInfo?)。找不到返回 (null, null)。
    /// </summary>
    private (ProjectInfo?, RunCommandInfo?) FindCommand(int commandId)
    {
        var registry = Registry;
        if (registry == null) return (null, null);

        foreach (var project in registry.GetAll())
        {
            // 注意：GetAll() 的 ProjectInfo.Commands 为空（仅 Get(id) 填充），必须按项目单独取命令。
            var cmd = registry.GetCommands(project.Id).FirstOrDefault(c => c.Id == commandId);
            if (cmd != null) return (project, cmd);
        }
        return (null, null);
    }

    /// <summary>
    /// 杀进程整树。Windows 用 taskkill /T /F（对 cmd 子进程比 Kill(entireProcessTree) 更可靠）。
    /// 其他系统用 <see cref="Process.Kill(bool)"/>。PILOT-087：失败时回传原因（退出码/异常摘要）
    /// 供 <see cref="StopOutcome.Failed"/> 展示；进程已不存在返回 false，由调用方归类为 AlreadyStopped。
    /// </summary>
    private static (bool Ok, string? Error) KillTree(int pid)
    {
        if (pid <= 0) return (false, $"非法 PID：{pid}");
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                if (proc.HasExited) return (false, "进程已退出");
                proc.Kill(entireProcessTree: true);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        try
        {
            var psi = new ProcessStartInfo("taskkill", $"/T /F /PID {pid}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return (false, "taskkill 启动失败");
            proc.WaitForExit(3000);
            return proc.ExitCode == 0
                ? (true, null)
                : (false, $"taskkill 退出码 {proc.ExitCode}（128=进程不存在，1=拒绝访问）");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private List<RunSession> AliveLaunched() =>
        _launched.Values.Where(s => IsProcessAlive(s.Pid)).ToList();

    /// <summary>
    /// 把检测会话并进列表：同 PID 或已由本页启动的同一命令（commandId ≠ 0）保留 Launched。
    /// <paramref name="replaceCache"/> 为 true 时，用并入结果重写 Detected 缓存（丢掉已退出的）。
    /// </summary>
    private List<RunSession> MergeDetected(
        List<RunSession> launched,
        IEnumerable<RunSession> detected,
        bool replaceCache)
    {
        var launchedPids = new HashSet<int>(launched.Select(s => s.Pid));
        var launchedCmdIds = new HashSet<int>(launched.Select(s => s.CommandId).Where(id => id != 0));
        var extras = new List<RunSession>();
        var seenPids = new HashSet<int>();
        foreach (var d in detected)
        {
            if (d.Pid <= 0 || !seenPids.Add(d.Pid)) continue;
            if (launchedPids.Contains(d.Pid)) continue;
            if (d.CommandId != 0 && launchedCmdIds.Contains(d.CommandId)) continue;
            if (!IsProcessAlive(d.Pid)) continue;
            extras.Add(d);
        }

        if (replaceCache)
        {
            _detected.Clear();
            foreach (var d in extras)
                _detected[d.Pid] = d;
        }

        var merged = new List<RunSession>(launched.Count + extras.Count);
        merged.AddRange(launched);
        merged.AddRange(extras);
        return merged;
    }

    /// <summary>按项目取已登记命令。GetAll() 不带 Commands，必须按项目单独取，且不改档案。</summary>
    private Dictionary<int, List<RunCommandInfo>> LoadCommandsByProject(IReadOnlyList<ProjectInfo> projects)
    {
        var map = new Dictionary<int, List<RunCommandInfo>>();
        var registry = Registry;
        if (registry == null) return map;
        foreach (var project in projects)
            map[project.Id] = registry.GetCommands(project.Id);
        return map;
    }

    private static RunSession ToDetectedSession(
        int pid,
        ProjectInfo project,
        string? exePath,
        string? cmdLine,
        string fallbackName,
        IReadOnlyDictionary<int, List<RunCommandInfo>> commandsByProject)
    {
        commandsByProject.TryGetValue(project.Id, out var commands);
        var command = ProcessMatcher.MatchCommand(exePath, cmdLine, commands ?? new List<RunCommandInfo>());
        var fallback = string.IsNullOrWhiteSpace(exePath)
            ? fallbackName
            : Path.GetFileNameWithoutExtension(exePath);
        if (string.IsNullOrWhiteSpace(fallback))
            fallback = fallbackName;
        return new RunSession
        {
            CommandId = command?.Id ?? 0,
            ProjectId = project.Id,
            ProjectName = project.Name,
            CommandName = command?.Name ?? fallback,
            Pid = pid,
            StartedAt = DateTime.MinValue,
            Origin = "Detected"
        };
    }

    /// <summary>按平台检测本机进程。检测失败时返回空列表，调用方仍保留 Launched。</summary>
    private List<RunSession> Detect()
    {
        try
        {
            return OperatingSystem.IsWindows() ? DetectViaWmi() : DetectViaProc();
        }
        catch (Exception)
        {
            return new List<RunSession>();
        }
    }

    private static string CommandFallback(string? exePath, string fallbackName)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return fallbackName;
        var name = Path.GetFileNameWithoutExtension(exePath);
        return string.IsNullOrWhiteSpace(name) ? fallbackName : name;
    }

    /// <summary>
    /// 已登记项目目录下、但类型对不上项目的进程。只留 pid（ProjectId 为 0）。
    /// 对得上项目的进程由 <see cref="ToDetectedSession"/> 写入，不走这里。
    /// 目录之外的进程返回 null，不进列表。
    /// </summary>
    private static RunSession? DetectedSession(
        int pid,
        string commandName,
        string? exePath,
        string? commandLine,
        string? cwd,
        IReadOnlyList<ProjectInfo> projects)
    {
        var matched = ProcessMatcher.MatchProject(exePath, commandLine, projects, cwd);
        if (matched != null)
        {
            return new RunSession
            {
                CommandId = 0,
                ProjectId = matched.Id,
                ProjectName = matched.Name,
                CommandName = commandName,
                Pid = pid,
                StartedAt = DateTime.MinValue,
                Origin = "Detected"
            };
        }
        if (!ProcessMatcher.LocatedUnderRegisteredRoot(exePath, commandLine, projects, cwd)) return null;
        return new RunSession
        {
            CommandId = 0,
            ProjectId = 0,
            ProjectName = "",
            CommandName = commandName,
            Pid = pid,
            StartedAt = DateTime.MinValue,
            Origin = "Detected"
        };
    }

    /// <summary>读 /proc/{pid}/exe、cmdline、cwd，按项目根归属判定。无 /proc 时返回空列表。</summary>
    private List<RunSession> DetectViaProc()
    {
        var projects = Registry?.GetAll() ?? new List<ProjectInfo>();
        var commandsByProject = LoadCommandsByProject(projects);
        var result = new List<RunSession>();
        if (!Directory.Exists("/proc")) return result;

        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            var name = Path.GetFileName(dir);
            if (!int.TryParse(name, out var pid) || pid <= 0) continue;
            try
            {
                var exePath = ReadProcLink(Path.Combine(dir, "exe"));
                var cwd = ReadProcLink(Path.Combine(dir, "cwd"));
                var cmdLine = ReadProcCmdline(Path.Combine(dir, "cmdline"));
                var matched = ProcessMatcher.MatchProject(exePath, cmdLine, projects, cwd);
                if (matched != null)
                {
                    result.Add(ToDetectedSession(pid, matched, exePath, cmdLine, fallbackName: name, commandsByProject));
                    continue;
                }
                var unmatched = DetectedSession(pid, CommandFallback(exePath, name), exePath, cmdLine, cwd, projects);
                if (unmatched != null) result.Add(unmatched);
            }
            catch
            {
                // 进程在枚举期间退出，或无权读该 pid：跳过这一条。
            }
        }

        return result;
    }

    private static string? ReadProcLink(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadProcCmdline(string path)
    {
        try
        {
            var raw = File.ReadAllBytes(path);
            if (raw.Length == 0) return null;
            return Encoding.UTF8.GetString(raw).Replace('\0', ' ').Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>WMI 查询全量进程并做归属判定，返回 Detected 会话。</summary>
    private List<RunSession> DetectViaWmi()
    {
        var registry = Registry;
        var projects = registry?.GetAll() ?? new List<ProjectInfo>();
        var commandsByProject = LoadCommandsByProject(projects);
        var result = new List<RunSession>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, Name, ExecutablePath, CommandLine FROM Win32_Process");
            using var coll = searcher.Get();
            foreach (ManagementObject mo in coll)
            {
                var pid = Convert.ToInt32(mo["ProcessId"]);
                var name = mo["Name"]?.ToString() ?? string.Empty;
                var exePath = mo["ExecutablePath"]?.ToString();
                var cmdLine = mo["CommandLine"]?.ToString();

                var matched = ProcessMatcher.MatchProject(exePath, cmdLine, projects);
                if (matched != null)
                {
                    result.Add(ToDetectedSession(pid, matched, exePath, cmdLine, fallbackName: name, commandsByProject));
                    continue;
                }
                var unmatched = DetectedSession(pid, Path.GetFileNameWithoutExtension(name), exePath, cmdLine, null, projects);
                if (unmatched != null) result.Add(unmatched);
            }
        }
        catch (Exception)
        {
            // WMI 不可用 → 降级：仅返回已有 Launched（CheckAndMerge 合并逻辑会处理）
            // 此处吞掉异常，调用方拿到空 Detected 列表。
        }

        return result;
    }
    #endregion
}
