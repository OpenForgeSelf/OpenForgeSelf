using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Entities;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Core;
using NewLife.Log;
using XCode;
// 让 XCode 生成的字段访问器 _.ProjectId / _.Stage 可以裸用（同 TodoService 里 Todo._.Status 的写法，这里只是省略前缀）
using static ForgeSelf.Api.Plugins.TodoTracker.Entities.Todo;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 项目关联实现（PILOT-054 · FR-2）。
///
/// 三条硬口径：
/// <list type="bullet">
/// <item><b>接缝每次现取</b>：<c>Registry</c> 是表达式属性（<c>_ctx.Get&lt;IProjectRegistry&gt;()</c>），
/// 不缓存实例 —— 提供方热重载后共享表条目会摘除，缓存即悬空（IProjectRegistry 头注 + architecture-design 铁律 4）。</item>
/// <item><b>同一性靠归一 Key</b>：宿主 <c>HostProjectRegistry</c> 用 <c>Path.GetFullPath</c> + 精确匹配
/// （只改宿主不解决问题：Git-Bash 写法会被解成 <c>C:\d\…</c> 直接判「目录不存在」）。
/// 用户拍板「插件内部支持多种格式」，故比对时把双方都跑一遍 <see cref="ProjectPathCanonicalizer"/>。</item>
/// <item><b>不删宿主档案</b>：只登记与读，解除关联仅清本插件任务上的三个投影字段。</item>
/// </list>
/// </summary>
public class TodoProjectService : ITodoProjectService
{
    /// <summary>本插件登记的项目档案来源标记。</summary>
    public const string RegisterSource = "todo-tracker";

    private const string SeamMissingMessage = "项目注册表不可用（宿主 IProjectRegistry 接缝未就绪）";

    private readonly IContext _ctx;

    public TodoProjectService(IContext ctx) => _ctx = ctx;

    private IProjectRegistry? Registry => _ctx.Get<IProjectRegistry>();

    /// <inheritdoc />
    public bool IsAvailable => Registry != null;

    /// <inheritdoc />
    public ProjectPathCanonicalizer.Result Canonicalize(string? path) => ProjectPathCanonicalizer.Normalize(path);

    /// <inheritdoc />
    public ResolveProjectResult Resolve(string? path, bool registerIfMissing)
    {
        var raw = (path ?? string.Empty).Trim();
        var canonical = ProjectPathCanonicalizer.Normalize(raw);
        if (!canonical.Ok) return ResolveProjectResult.Failed(canonical.Error!, raw);

        var root = canonical.Root;

        if (!Directory.Exists(root))
            return ResolveProjectResult.Failed($"目录不存在：{root}", raw);

        var registry = Registry;
        if (registry == null)
            return ResolveProjectResult.Failed(SeamMissingMessage, raw, available: false);

        var projects = SafeGetAll(registry);
        var hit = Match(projects, canonical.Key);
        if (hit != null) return Success(hit, raw, root, canonical.Key, registered: false);

        if (!registerIfMissing) return Success(null, raw, root, canonical.Key, registered: false);

        if (!registry.Register(root, RegisterSource, out var registerError))
            return ResolveProjectResult.Failed(registerError ?? $"登记项目失败：{root}", raw);

        // 登记后立即直查回读（宿主 Register 内部 Insert 同步完成，GetAll 读得到）。
        // 读不回就如实失败：绝不能把"没匹配上"当成"关联成功"，否则任务会挂到不存在的项目上。
        projects = SafeGetAll(registry);
        hit = Match(projects, canonical.Key);
        if (hit == null)
            return ResolveProjectResult.Failed($"已登记但未能读回项目档案：{root}（请重试）", raw);

        XTrace.Log.Info("[todo-tracker] 新项目已登记：#{0} {1}", hit.Id, root);
        return Success(hit, raw, root, canonical.Key, registered: true);
    }

    /// <inheritdoc />
    public TodoProjectDto? Get(int projectId)
    {
        if (projectId <= 0) return null;
        var registry = Registry;
        if (registry == null) return null;

        var info = SafeGet(registry, projectId);
        if (info == null) return null;

        var counts = TaskCounts();
        return ToDto(info, counts);
    }

    /// <inheritdoc />
    public IReadOnlyList<TodoProjectDto> ListProjects()
    {
        var registry = Registry;
        if (registry == null) return [];

        var counts = TaskCounts();
        return SafeGetAll(registry).Select(p => ToDto(p, counts)).ToList();
    }

    /// <inheritdoc />
    public Dictionary<int, string> NamesFor(IEnumerable<int> projectIds)
    {
        var result = new Dictionary<int, string>();
        var wanted = projectIds.Where(id => id > 0).Distinct().ToHashSet();
        if (wanted.Count == 0) return result;

        foreach (var project in SafeGetAll(Registry))
        {
            if (wanted.Contains(project.Id)) result[project.Id] = project.Name;
        }
        return result;
    }

    /// <summary>
    /// 在宿主项目清单里找归一 Key 相同的那条。
    /// 比对前对<b>宿主存的 Root 也跑一遍归一</b>：宿主历史行可能是别的写法（如带尾分隔符、小写盘符），
    /// 直接字符串相等会把同一个项目判成两个。
    /// </summary>
    private static ProjectInfo? Match(List<ProjectInfo> projects, string key)
    {
        foreach (var project in projects)
        {
            var hostKey = ProjectPathCanonicalizer.KeyOf(project.Root);
            if (hostKey != null && string.Equals(hostKey, key, StringComparison.Ordinal)) return project;
        }
        return null;
    }

    private static ResolveProjectResult Success(ProjectInfo? hit, string raw, string root, string key, bool registered) => new()
    {
        Ok = true,
        Root = root,
        Key = key,
        Raw = raw,
        ProjectId = hit?.Id ?? 0,
        ProjectName = hit?.Name ?? string.Empty,
        Registered = registered,
        RegistryAvailable = true
    };

    /// <summary>
    /// 一次查询取回全部已关联任务并在内存计数（逐项目查即 N+1）。
    /// 不做字段投影：XCode 的 select 只吃字符串列名（传 Field 会编译不过），而本表规模是"一人/一队的项目数"，
    /// 读整行的代价远低于把列名写错的风险。
    /// </summary>
    private static Dictionary<int, (int Total, int Open)> TaskCounts()
    {
        var map = new Dictionary<int, (int Total, int Open)>();
        foreach (var row in Todo.FindAll(_.ProjectId > 0))
        {
            var (total, open) = map.TryGetValue(row.ProjectId, out var v) ? v : (0, 0);
            map[row.ProjectId] = (total + 1, open + (row.Stage == TodoStage.Done || row.Stage == TodoStage.Cancelled ? 0 : 1));
        }
        return map;
    }

    private static TodoProjectDto ToDto(ProjectInfo info, Dictionary<int, (int Total, int Open)> counts)
    {
        var (total, open) = counts.TryGetValue(info.Id, out var c) ? c : (0, 0);
        return new TodoProjectDto
        {
            Id = info.Id,
            Root = info.Root,
            Key = ProjectPathCanonicalizer.KeyOf(info.Root) ?? info.Root.ToUpperInvariant(),
            Name = info.Name,
            Type = info.Type,
            Description = info.Description,
            Tags = SplitTags(info.Tags),
            Source = info.Source,
            PathExists = info.PathExists,
            IsGitRepo = info.IsGitRepo,
            TaskCount = total,
            OpenTaskCount = open,
            LastActiveAt = info.LastActiveAt
        };
    }

    private static List<string> SplitTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // 注册表是宿主提供的软依赖：调用失败（库锁、热重载窗口）时降级为空清单而非 500 ——
    // 待办列表本身仍应可用，项目名留空由界面提示。
    private static List<ProjectInfo> SafeGetAll(IProjectRegistry? registry)
    {
        if (registry == null) return [];
        try { return registry.GetAll(); }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[todo-tracker] 读宿主项目注册表失败（降级为空清单）：{0}", ex.Message);
            return [];
        }
    }

    private static ProjectInfo? SafeGet(IProjectRegistry registry, int id)
    {
        try { return registry.Get(id); }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[todo-tracker] 读项目 #{0} 失败：{1}", id, ex.Message);
            return null;
        }
    }
}
