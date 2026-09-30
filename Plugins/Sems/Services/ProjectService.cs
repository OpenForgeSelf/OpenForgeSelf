using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.Sems.Services;

/// <summary>sems 操作结果状态（控制器与工具据此映射 HTTP 码 / 工具错误，不再靠字符串嗅探）。</summary>
public enum SemsOpStatus
{
    /// <summary>成功。</summary>
    Ok,
    /// <summary>入参非法（缺字段、目录不存在等）。</summary>
    Invalid,
    /// <summary>目标不存在。</summary>
    NotFound,
    /// <summary>状态冲突（如项目仍有存活运行会话）。</summary>
    Conflict,
    /// <summary>宿主项目注册表接缝不可用（提供方缺席）。</summary>
    Unavailable
}

/// <summary>
/// sems 统一操作结果。HTTP 控制器据此选状态码，MCP 工具据此出 <c>{success,data|error}</c> 封套 ——
/// 同一份语义，两个消费面（一处真相）。
/// </summary>
public class SemsResult<T>
{
    /// <summary>状态。</summary>
    public SemsOpStatus Status { get; init; } = SemsOpStatus.Ok;

    /// <summary>失败原因（中文，可直接展示给用户/外部调用方）。</summary>
    public string? Error { get; init; }

    /// <summary>结果数据（失败时为默认值）。</summary>
    public T? Data { get; init; }

    /// <summary>是否成功。</summary>
    public bool Success => Status == SemsOpStatus.Ok;

    /// <summary>映射 HTTP 状态码（控制器唯一依据）。</summary>
    public int StatusCode => Status switch
    {
        SemsOpStatus.Ok => 200,
        SemsOpStatus.Invalid => 400,
        SemsOpStatus.NotFound => 404,
        SemsOpStatus.Conflict => 409,
        SemsOpStatus.Unavailable => 503,
        _ => 400
    };

    /// <summary>成功。</summary>
    public static SemsResult<T> Ok(T data) => new() { Status = SemsOpStatus.Ok, Data = data };

    /// <summary>失败。</summary>
    public static SemsResult<T> Fail(SemsOpStatus status, string? error) => new() { Status = status, Error = error };
}

/// <summary>目录浏览条目（仅目录，不含文件）。</summary>
public class DirectoryEntry
{
    /// <summary>目录名（盘符根条目为盘符，如 "C:\\"）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>绝对路径。</summary>
    public string Path { get; set; } = string.Empty;
}

/// <summary>一次目录浏览结果。</summary>
public class DirectoryListing
{
    /// <summary>当前目录绝对路径；列举驱动器根时为 null。</summary>
    public string? Path { get; set; }

    /// <summary>上级目录绝对路径；已无上级（盘符根 / 列举驱动器）时为 null。</summary>
    public string? Parent { get; set; }

    /// <summary>子目录列表（按名称不区分大小写升序）。</summary>
    public List<DirectoryEntry> Directories { get; set; } = new();
}

/// <summary>
/// sems 项目工作区服务：项目与运行命令的<b>全部业务操作唯一落点</b>。
/// 自带界面（HTTP 控制器）与对外工具（<c>ToolExtensions</c>，经 McpCenter 暴露）都只消费本服务，
/// 因此「插件内部自洽」= 不依赖任何其他插件的动作即可完成登记/编辑/移除/命令维护/目录浏览。
/// 数据仍归宿主 <see cref="IProjectRegistry"/> 接缝（见 docs/01-architecture/host-capability-seams.md §4.1 方案 B）：
/// 构造只注入 <see cref="IContext"/> 与 <see cref="IRunnerService"/>，<b>每次调用经 ctx.Get 重新解析接缝</b>，
/// 不把解析到的实例缓存为字段（提供方热重载后共享表自动摘除，null 走降级）。
/// 旧的 JSON 直读逻辑已移除——JSON 仅作为宿主实现（HostProjectRegistry）的一次性迁移源。
/// </summary>
public interface IProjectService
{
    /// <summary>宿主项目注册表接缝当前是否可用（控制器据此先行返回 503，工具据此回错误封套）。</summary>
    bool IsAvailable { get; }

    /// <summary>全部已登记项目（按最近活动倒序）。接缝不可用返回空列表（降级，不抛）。</summary>
    List<ProjectInfo> GetProjects();

    /// <summary>项目数（供首页统计）。</summary>
    int Count { get; }

    /// <summary>
    /// 插件内部登记一个项目（来源记 <c>manual</c>）。Root 已存在则只刷新活跃时间（不覆写用户已编辑的档案）。
    /// <paramref name="name"/> 非空时覆盖显示名。
    /// </summary>
    SemsResult<ProjectInfo> Register(string root, string? name = null);

    /// <summary>档案编辑（Name/Type/Description/Tags）。</summary>
    SemsResult<bool> UpdateProject(int id, ProjectUpdate update);

    /// <summary>
    /// 移除项目档案（级联删除其运行命令；不触碰磁盘）。
    /// 该项目仍有本面板启动的存活会话时拒绝（Conflict），且不做任何数据变更。
    /// </summary>
    SemsResult<bool> RemoveProject(int id);

    /// <summary>某项目的运行命令列表（按 Sort 升序）。</summary>
    SemsResult<List<RunCommandInfo>> GetCommands(int projectId);

    /// <summary>新增运行命令，返回新命令 Id。</summary>
    SemsResult<int> AddCommand(int projectId, RunCommandInfo command);

    /// <summary>编辑运行命令。</summary>
    SemsResult<bool> UpdateCommand(int commandId, RunCommandUpdate update);

    /// <summary>删除运行命令。</summary>
    SemsResult<bool> DeleteCommand(int commandId);

    /// <summary>
    /// 目录浏览（供「添加项目」选择目录）：省略 <paramref name="path"/> 列举本机驱动器，
    /// 否则列举该目录的<b>直接子目录</b>。只读、不列文件、不递归。
    /// </summary>
    SemsResult<DirectoryListing> Browse(string? path);
}

public class ProjectService : IProjectService
{
    private const string UnavailableMessage = "项目注册表不可用（宿主接缝未就绪）";

    private readonly IContext _ctx;
    private readonly IRunnerService _runner;

    public ProjectService(IContext ctx, IRunnerService runner)
    {
        _ctx = ctx;
        _runner = runner;
    }

    private IProjectRegistry? Registry => _ctx.Get<IProjectRegistry>();

    public bool IsAvailable => Registry != null;

    public int Count => GetProjects().Count;

    /// <summary>
    /// 全部项目 + 每项目运行命令概要。
    /// 宿主 <see cref="IProjectRegistry.GetAll"/> 不附带 Commands（仅 Get(id) 附带），
    /// 故此处逐项补齐——这是前端统计卡、运行面板快捷访问 url 映射与「启动全部」的唯一数据源，
    /// 缺它会让三处功能静默失效（详见 docs/ai/pilot/sems-selfcontained-mcp-tools/03-plan.md 偏差记录）。
    /// </summary>
    public List<ProjectInfo> GetProjects()
    {
        var registry = Registry;
        if (registry == null) return new List<ProjectInfo>();

        var all = registry.GetAll();
        foreach (var project in all)
            project.Commands = registry.GetCommands(project.Id);
        return all;
    }

    public SemsResult<ProjectInfo> Register(string root, string? name = null)
    {
        var registry = Registry;
        if (registry == null) return SemsResult<ProjectInfo>.Fail(SemsOpStatus.Unavailable, UnavailableMessage);

        if (!registry.Register(root, "manual", out var registerError))
            return SemsResult<ProjectInfo>.Fail(SemsOpStatus.Invalid, registerError ?? "登记失败");

        var project = Locate(registry, root);
        if (project == null)
            return SemsResult<ProjectInfo>.Fail(SemsOpStatus.NotFound, $"登记成功但未能读回项目：{root}");

        var wanted = name?.Trim();
        if (!string.IsNullOrWhiteSpace(wanted) && wanted != project.Name)
        {
            if (registry.Update(project.Id, new ProjectUpdate { Name = wanted }))
                project = registry.Get(project.Id) ?? project;
        }

        return SemsResult<ProjectInfo>.Ok(project);
    }

    public SemsResult<bool> UpdateProject(int id, ProjectUpdate update)
    {
        var registry = Registry;
        if (registry == null) return SemsResult<bool>.Fail(SemsOpStatus.Unavailable, UnavailableMessage);

        if (!registry.Update(id, update))
            return SemsResult<bool>.Fail(SemsOpStatus.NotFound, $"项目不存在：{id}");

        return SemsResult<bool>.Ok(true);
    }

    public SemsResult<bool> RemoveProject(int id)
    {
        var registry = Registry;
        if (registry == null) return SemsResult<bool>.Fail(SemsOpStatus.Unavailable, UnavailableMessage);

        if (registry.Get(id) == null)
            return SemsResult<bool>.Fail(SemsOpStatus.NotFound, $"项目不存在：{id}");

        // 存活会话守卫：有本面板启动的进程在跑时绝不删档（被拒时零数据变更）
        var alive = _runner.Current().Count(s => s.ProjectId == id);
        if (alive > 0)
            return SemsResult<bool>.Fail(SemsOpStatus.Conflict,
                $"项目有 {alive} 个运行中的命令，请先停止后再移除");

        if (!registry.Remove(id, out var error))
            return SemsResult<bool>.Fail(SemsOpStatus.NotFound, error ?? "移除失败");

        return SemsResult<bool>.Ok(true);
    }

    public SemsResult<List<RunCommandInfo>> GetCommands(int projectId)
    {
        var registry = Registry;
        if (registry == null) return SemsResult<List<RunCommandInfo>>.Fail(SemsOpStatus.Unavailable, UnavailableMessage);

        if (registry.Get(projectId) == null)
            return SemsResult<List<RunCommandInfo>>.Fail(SemsOpStatus.NotFound, $"项目不存在：{projectId}");

        return SemsResult<List<RunCommandInfo>>.Ok(registry.GetCommands(projectId));
    }

    public SemsResult<int> AddCommand(int projectId, RunCommandInfo command)
    {
        var registry = Registry;
        if (registry == null) return SemsResult<int>.Fail(SemsOpStatus.Unavailable, UnavailableMessage);

        if (registry.Get(projectId) == null)
            return SemsResult<int>.Fail(SemsOpStatus.NotFound, $"项目不存在：{projectId}");

        if (command == null || string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Script))
            return SemsResult<int>.Fail(SemsOpStatus.Invalid, "命令名称与脚本均不能为空");

        var newId = registry.AddCommand(projectId, command);
        if (newId <= 0)
            return SemsResult<int>.Fail(SemsOpStatus.Invalid, "新增命令失败");

        return SemsResult<int>.Ok(newId);
    }

    public SemsResult<bool> UpdateCommand(int commandId, RunCommandUpdate update)
    {
        var registry = Registry;
        if (registry == null) return SemsResult<bool>.Fail(SemsOpStatus.Unavailable, UnavailableMessage);

        if (!registry.UpdateCommand(commandId, update))
            return SemsResult<bool>.Fail(SemsOpStatus.NotFound, $"命令不存在：{commandId}");

        return SemsResult<bool>.Ok(true);
    }

    public SemsResult<bool> DeleteCommand(int commandId)
    {
        var registry = Registry;
        if (registry == null) return SemsResult<bool>.Fail(SemsOpStatus.Unavailable, UnavailableMessage);

        if (!registry.DeleteCommand(commandId))
            return SemsResult<bool>.Fail(SemsOpStatus.NotFound, $"命令不存在：{commandId}");

        return SemsResult<bool>.Ok(true);
    }

    public SemsResult<DirectoryListing> Browse(string? path)
    {
        var listing = new DirectoryListing();

        if (string.IsNullOrWhiteSpace(path))
        {
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                    listing.Directories.Add(new DirectoryEntry { Name = drive.Name, Path = drive.Name });
            }
            catch (Exception ex)
            {
                return SemsResult<DirectoryListing>.Fail(SemsOpStatus.Invalid, $"无法枚举驱动器：{ex.Message}");
            }

            listing.Directories = listing.Directories.OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToList();
            return SemsResult<DirectoryListing>.Ok(listing);
        }

        string full;
        try
        {
            full = System.IO.Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch (Exception ex)
        {
            return SemsResult<DirectoryListing>.Fail(SemsOpStatus.Invalid, $"路径非法：{ex.Message}");
        }

        if (!Directory.Exists(full))
            return SemsResult<DirectoryListing>.Fail(SemsOpStatus.Invalid, $"目录不存在：{full}");

        string[] children;
        try
        {
            children = Directory.GetDirectories(full);
        }
        catch (UnauthorizedAccessException)
        {
            return SemsResult<DirectoryListing>.Fail(SemsOpStatus.Invalid, $"无权限访问：{full}");
        }
        catch (Exception ex)
        {
            return SemsResult<DirectoryListing>.Fail(SemsOpStatus.Invalid, $"读取目录失败：{ex.Message}");
        }

        listing.Path = full;
        listing.Parent = new DirectoryInfo(full).Parent?.FullName;
        foreach (var child in children.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
        {
            var name = System.IO.Path.GetFileName(child);
            if (string.IsNullOrEmpty(name)) continue; // 盘符根等无 basename 的条目不列
            listing.Directories.Add(new DirectoryEntry { Name = name, Path = child });
        }

        return SemsResult<DirectoryListing>.Ok(listing);
    }

    /// <summary>
    /// 登记后读回项目：宿主已在 Register 内把 Root 归一化，故按归一化路径不区分大小写匹配；
    /// 匹配不到（并发登记等极端情形）退化为「最近活动」那条，绝不返回 null 掩盖成功。
    /// </summary>
    private static ProjectInfo? Locate(IProjectRegistry registry, string root)
    {
        var all = registry.GetAll();
        string? normalized = null;
        try
        {
            normalized = System.IO.Path.GetFullPath(root.Trim().Trim('"'));
        }
        catch
        {
            // 归一化失败走退化分支
        }

        if (normalized != null)
        {
            var hit = all.FirstOrDefault(p => string.Equals(p.Root, normalized, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }

        return all.FirstOrDefault();
    }
}
