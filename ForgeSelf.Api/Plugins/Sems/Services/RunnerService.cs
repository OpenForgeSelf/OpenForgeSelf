// CA1416: 本文件 WMI 调用（System.Management）仅运行于 windows 宿主。
// 宿主为 Windows 桌面程序（exe 发布），sems 插件 TargetFramework=net10.0（非 windows），
// 故 WMI API 触发「仅 windows 支持」警告。WMI 进程检测本就是宿主运行时能力，运行时验收环境为 Windows，
// 不跨平台，故在本文件作用域禁用 CA1416（不在 sems.csproj 层禁用，避免掩盖其他真问题）。
#pragma warning disable CA1416
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.Sems.Services;

/// <summary>
/// 运行会话（Launched 面板启动 / Detected WMI 检测捕获）。
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

    /// <summary>来源：Launched（本面板启动） / Detected（WMI 检测捕获）。</summary>
    public string Origin { get; set; } = "Launched";
}

/// <summary>
/// sems 进程运行管理（design §5 / §1.2）：进程启动 / 停止 / 本机进程检测合并。
/// 插件内服务，不进 Abstractions。经 <c>IContext.Get&lt;IProjectRegistry&gt;()</c> 读取命令与项目根，
/// 每次使用每次解析（不缓存接缝实例为字段，符合宿主契约消费铁律）。
/// 会话表内存维护，宿主重启清空（NFR-3）。
/// </summary>
public interface IRunnerService
{
    /// <summary>在项目根目录执行 cmd /c &lt;script&gt;，记录会话。同 commandId 已存活则返回 null（控制器回 409）。</summary>
    RunSession? Launch(int commandId);

    /// <summary>停止「本面板启动」的会话（按 commandId 取 PID 杀整树）。</summary>
    bool Stop(int commandId);

    /// <summary>停止外部捕获进程（按 PID 杀整树）。</summary>
    bool StopExternal(int pid);

    /// <summary>WMI 检测 + 与 Launched 合并，返回当前运行列表。</summary>
    IReadOnlyList<RunSession> CheckAndMerge();

    /// <summary>当前运行列表（Launched + Detected）。</summary>
    IReadOnlyList<RunSession> Current();

    /// <summary>停止全部本面板启动的会话（清理/测试用）。</summary>
    void StopAll();
}

public class RunnerService : IRunnerService
{
    private readonly IContext _ctx;
    // Launched 会话：commandId → 会话。进程 Exited 时移除。
    private readonly ConcurrentDictionary<int, RunSession> _launched = new();

    public RunnerService(IContext ctx)
    {
        _ctx = ctx;
    }

    private IProjectRegistry? Registry => _ctx.Get<IProjectRegistry>();

    /// <summary>
    /// 在命令所属项目根目录启动进程。返回会话（含 PID），若 commandId 已存活或找不到命令则返回 null。
    /// </summary>
    public RunSession? Launch(int commandId)
    {
        // 重复启动拒绝
        if (_launched.ContainsKey(commandId)) return null;

        var (project, command) = FindCommand(commandId);
        if (project == null || command == null) return null;

        var script = command.Script;
        if (string.IsNullOrWhiteSpace(script)) return null;

        var root = project.Root;
        if (!Directory.Exists(root)) return null;

        var psi = new ProcessStartInfo("cmd.exe", $"/c {script}")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process process;
        try
        {
            process = Process.Start(psi) ?? throw new InvalidOperationException("进程启动失败：Process.Start 返回 null");
        }
        catch (Exception)
        {
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

        // 进程退出时从会话表移除
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            _launched.TryRemove(commandId, out _);
            try { process.Dispose(); } catch { }
        };

        _launched[commandId] = session;
        return session;
    }

    /// <summary>停止本面板启动的会话：按 commandId 取 PID，杀整树。</summary>
    public bool Stop(int commandId)
    {
        if (!_launched.TryGetValue(commandId, out var session)) return false;
        var ok = KillTree(session.Pid);
        // 同步移除会话（Exited 事件作为异步兜底），保证 Current() 立即反映停止状态。
        _launched.TryRemove(commandId, out _);
        return ok;
    }

    /// <summary>停止外部捕获进程（按 PID 杀整树）。进程不存在/权限不足返回 false。</summary>
    public bool StopExternal(int pid)
    {
        if (pid <= 0) return false;
        return KillTree(pid);
    }

    /// <summary>WMI 检测全量进程 + 与 Launched 合并。</summary>
    public IReadOnlyList<RunSession> CheckAndMerge()
    {
        var detected = DetectViaWmi();
        var merged = new List<RunSession>(_launched.Values);
        var launchedPids = new HashSet<int>(_launched.Values.Select(s => s.Pid));
        foreach (var d in detected)
        {
            if (launchedPids.Contains(d.Pid)) continue; // PID 相同 → 保留 Launched，不重复
            merged.Add(d);
        }
        return merged;
    }

    /// <summary>当前运行列表（Launched + 上次检测的 Detected 缓存）。</summary>
    public IReadOnlyList<RunSession> Current()
    {
        // 过滤掉已退出但事件尚未触发的 Launched（兜底）
        var alive = _launched.Values
            .Where(s => IsProcessAlive(s.Pid))
            .ToList();
        return alive.AsReadOnly();
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

    /// <summary>杀进程整树（Windows：优先 taskkill /T /F，比 .NET Kill(entireProcessTree) 对 cmd 子进程更可靠）。</summary>
    private static bool KillTree(int pid)
    {
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
            if (proc == null) return false;
            proc.WaitForExit(3000);
            // taskkill 退出码 0=成功杀树；非0（进程不存在/权限不足）视为操作未成功
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
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

    /// <summary>WMI 查询全量进程并做归属判定，返回 Detected 会话。</summary>
    private List<RunSession> DetectViaWmi()
    {
        var registry = Registry;
        var projects = registry?.GetAll() ?? new List<ProjectInfo>();
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
                if (matched == null) continue;

                result.Add(new RunSession
                {
                    CommandId = 0,
                    ProjectId = matched.Id,
                    ProjectName = matched.Name,
                    CommandName = Path.GetFileNameWithoutExtension(name),
                    Pid = pid,
                    StartedAt = DateTime.MinValue, // WMI 无精确启动时间，留空
                    Origin = "Detected"
                });
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
