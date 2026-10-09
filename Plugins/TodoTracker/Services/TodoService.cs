using ForgeSelf.Api.Plugins.TodoTracker.Entities;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using static ForgeSelf.Api.Plugins.TodoTracker.Entities.Todo;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 任务主服务（PILOT-054 扩展）。
///
/// 三条既有口径继续成立：
/// <list type="bullet">
/// <item><b>分页 total 真取</b>（<c>RetrieveTotalCount=true</c>）——本插件当初就是为修这个而立的样例；</item>
/// <item><b>校验异常抛 ArgumentException</b>，控制器映射 400；其它异常如实冒泡 500（plugin-development §C）；</item>
/// <item><b>存在性判断直查库</b>（<c>FindByKey</c> 用 <c>FindAll</c> 而非实体缓存，铁律 11）。</item>
/// </list>
/// 兼容：旧出参 8 键与旧端点语义不变（<c>Home</c> 插件与既有 e2e 在消费）。
/// </summary>
public class TodoService : ITodoService
{
    private const int MaxPageSize = 100;
    private const int MaxTitleLength = 200;
    private const int MaxRemarkLength = 1000;

    private readonly ITodoProjectService _projects;
    private readonly ITaskExecutionService _records;

    public TodoService(ITodoProjectService projects, ITaskExecutionService records)
    {
        _projects = projects;
        _records = records;
    }

    /// <summary>
    /// 无依赖构造（既有 <c>new TodoService()</c> 调用点：控制器集成测试）。
    /// 项目能力按「缺席」处理 —— 解析一律如实失败并说明原因，而不是假装关联成功；
    /// 因此这条构造只覆盖<b>不关联项目</b>的旧路径，涉项目的用例必须走 DI 注入的版本。
    /// </summary>
    public TodoService() : this(NoProjects.Instance, new TaskExecutionService())
    {
    }

    /// <summary>项目能力的空实现（无依赖构造专用）。</summary>
    private sealed class NoProjects : ITodoProjectService
    {
        internal static readonly NoProjects Instance = new();

        private const string Message = "未注入宿主项目注册表（本构造用于无依赖测试），项目关联不可用";

        public bool IsAvailable => false;

        public ProjectPathCanonicalizer.Result Canonicalize(string? path) => ProjectPathCanonicalizer.Normalize(path);

        public ResolveProjectResult Resolve(string? path, bool registerIfMissing) =>
            ResolveProjectResult.Failed(Message, (path ?? string.Empty).Trim(), available: false);

        public TodoProjectDto? Get(int projectId) => null;

        public IReadOnlyList<TodoProjectDto> ListProjects() => [];

        public Dictionary<int, string> NamesFor(IEnumerable<int> projectIds) => [];
    }

    public Task<PagedResult<TodoDto>> GetTodosAsync(string? status = null, int page = 1, int pageSize = 20) =>
        GetTodosAsync(new TaskQuery { Status = status, Page = page, PageSize = pageSize });

    public Task<PagedResult<TodoDto>> GetTodosAsync(TaskQuery query)
    {
        query ??= new TaskQuery();

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => query.PageSize
        };

        XTrace.Log.Debug("获取待办列表，status={0}, stage={1}, projectId={2}, q={3}, page={4}, pageSize={5}",
            query.Status, query.Stage, query.ProjectId, query.Keyword, page, pageSize);

        var exp = new WhereExpression();

        var statusValue = TodoStatus.ToValue(query.Status);
        if (statusValue.HasValue) exp &= _.Status == statusValue.Value;
        if (query.Stage.HasValue) exp &= _.Stage == query.Stage.Value;
        if (query.ProjectId > 0) exp &= _.ProjectId == query.ProjectId;
        if (query.UnlinkedOnly) exp &= _.ProjectId == 0;

        var keyword = (query.Keyword ?? string.Empty).Trim();
        if (keyword.Length > 0)
            exp &= (_.Title.Contains(keyword) | _.Remark.Contains(keyword) | _.Objective.Contains(keyword));

        var pageParam = new PageParameter
        {
            PageIndex = page - 1,
            PageSize = pageSize,
            Sort = "CreatedAt DESC, Id DESC",
            RetrieveTotalCount = true
        };

        var list = Todo.FindAll(exp, pageParam);
        var total = (int)pageParam.TotalCount;

        // 批量补齐：项目名一次读注册表，记录数一次查这页的 id（逐行查即 N+1）
        var names = _projects.NamesFor(list.Select(t => t.ProjectId));
        var counts = TaskExecution.CountByTasks(list.Select(t => t.Id));

        var items = list
            .Select(t => TodoProjection.ToDto(t, names.Pick(t.ProjectId), counts.Pick(t.Id)))
            .ToList();

        XTrace.Log.Info("获取待办列表成功，总数: {0}, 当前页数量: {1}", total, items.Count);

        return Task.FromResult(new PagedResult<TodoDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public Task<TodoDto?> GetTodoByIdAsync(int id)
    {
        XTrace.Log.Debug("获取待办详情，id={0}", id);
        if (id <= 0) return Task.FromResult<TodoDto?>(null);

        var todo = Todo.FindById(id);
        return Task.FromResult(todo == null ? null : Project(todo));
    }

    public Task<TodoDto?> GetTodoByKeyAsync(string? taskKey)
    {
        var key = Todo.NormalizeKey(taskKey);
        if (key.Length == 0) return Task.FromResult<TodoDto?>(null);

        var todo = Todo.FindByKey(key);
        return Task.FromResult(todo == null ? null : Project(todo));
    }

    public async Task<TodoDto> CreateTodoAsync(CreateTodoRequest request)
    {
        XTrace.Log.Info("创建待办，Title={0}", request.Title);

        ValidateTitle(request.Title);
        ValidateRemark(request.Remark);

        var now = DateTime.Now;
        var todo = new Todo
        {
            Title = request.Title.Trim(),
            Remark = request.Remark,
            Status = TodoStatus.PendingValue,
            DueDate = request.DueDate ?? DateTime.MinValue,
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = DateTime.MinValue,
            TaskKey = Guid.NewGuid().ToString("N"),
            Priority = request.Priority is >= 1 and <= 3 ? request.Priority.Value : 1,
            Objective = (request.Objective ?? string.Empty).Trim(),
            Content = request.Content ?? string.Empty,
            AllowedScope = request.AllowedScope ?? string.Empty,
            ForbiddenScope = request.ForbiddenScope ?? string.Empty,
            Acceptance = request.Acceptance ?? string.Empty,
            Verification = request.Verification ?? string.Empty,
            Assignee = (request.Assignee ?? string.Empty).Trim(),
            PermissionMode = "read-only"
        };

        if (TodoStage.TryParse(request.Stage, out var stage)) todo.Stage = stage;
        // 建单时随手写的路径不登记宿主档案（不污染项目清单），要登记走显式「关联项目」
        ApplyProjectFields(todo, request.ProjectPath, request.ProjectId, registerIfMissing: false);

        await todo.InsertAsync();

        XTrace.Log.Info("待办创建成功，Id={0} Key={1}", todo.Id, todo.TaskKey);
        return TodoProjection.ToDto(todo, ProjectName(todo.ProjectId));
    }

    public async Task<TodoDto?> UpdateTodoAsync(int id, UpdateTodoRequest request)
    {
        XTrace.Log.Info("更新待办，id={0}", id);

        var todo = Todo.FindById(id);
        if (todo == null) return null;
        if (request == null) return TodoProjection.ToDto(todo);

        // 部分更新：null=不改，空串=清空（内容类字段允许清空回草稿）
        if (request.Title != null)
        {
            ValidateTitle(request.Title);
            todo.Title = request.Title.Trim();
        }
        if (request.Remark != null)
        {
            ValidateRemark(request.Remark);
            todo.Remark = request.Remark;
        }
        if (request.DueDate.HasValue) todo.DueDate = request.DueDate.Value;
        if (request.Priority is >= 1 and <= 3) todo.Priority = request.Priority.Value;

        if (request.Objective != null) todo.Objective = request.Objective.Trim();
        if (request.Content != null) todo.Content = request.Content;
        if (request.AllowedScope != null) todo.AllowedScope = request.AllowedScope;
        if (request.ForbiddenScope != null) todo.ForbiddenScope = request.ForbiddenScope;
        if (request.Acceptance != null) todo.Acceptance = request.Acceptance;
        if (request.Verification != null) todo.Verification = request.Verification;
        if (request.Assignee != null) todo.Assignee = request.Assignee.Trim();

        await todo.UpdateAsync();
        return TodoProjection.ToDto(todo, ProjectName(todo.ProjectId));
    }

    public async Task<bool> DeleteTodoAsync(int id)
    {
        XTrace.Log.Info("删除待办，id={0}", id);

        var todo = Todo.FindById(id);
        if (todo == null) return false;

        await todo.DeleteAsync();
        XTrace.Log.Info("待办删除成功，Id={0}", id);
        return true;
    }

    public async Task<TodoDto?> CompleteTodoAsync(int id, string actor = "rest")
    {
        XTrace.Log.Info("标记待办完成，id={0}", id);

        var todo = Todo.FindById(id);
        if (todo == null) return null;

        if (todo.Status != TodoStatus.CompletedValue || todo.Stage != TodoStage.Done)
        {
            var from = todo.Stage;
            todo.Status = TodoStatus.CompletedValue;
            todo.Stage = TodoStage.Done;
            todo.CompletedAt = DateTime.Now;
            todo.UpdatedAt = DateTime.Now;
            await todo.UpdateAsync();

            // 留痕：完成动作必须可回放（PILOT-055 P1，对齐 ChangeStageInternalAsync 模式）
            await _records.AppendSystemAsync(todo.Id, actor, "标记完成",
                stageFrom: from, stageTo: TodoStage.Done, result: $"状态：{TodoStatus.CompletedName}");

            XTrace.Log.Info("待办标记完成成功，Id={0}", id);
        }

        return TodoProjection.ToDto(todo, ProjectName(todo.ProjectId));
    }

    public async Task<TodoDto?> ReopenTodoAsync(int id, string actor = "rest")
    {
        XTrace.Log.Info("重新打开待办，id={0}", id);

        var todo = Todo.FindById(id);
        if (todo == null) return null;

        if (todo.Status != TodoStatus.PendingValue || todo.Stage != TodoStage.Draft)
        {
            var from = todo.Stage;
            todo.Status = TodoStatus.PendingValue;
            todo.Stage = TodoStage.Draft;
            todo.CompletedAt = DateTime.MinValue;
            todo.UpdatedAt = DateTime.Now;
            await todo.UpdateAsync();

            // 留痕：重开动作必须可回放（PILOT-055 P1，对齐 ChangeStageInternalAsync 模式）
            await _records.AppendSystemAsync(todo.Id, actor, "重新打开",
                stageFrom: from, stageTo: TodoStage.Draft, result: $"状态：{TodoStatus.PendingName}");

            XTrace.Log.Info("待办重新打开成功，Id={0}", id);
        }

        return TodoProjection.ToDto(todo, ProjectName(todo.ProjectId));
    }

    public Task<TodoOpResult> ChangeStageAsync(int id, StageChangeRequest request, string actor) =>
        ChangeStageInternalAsync(() => Todo.FindById(id), id, request, actor);

    public Task<TodoOpResult> ChangeStageByKeyAsync(string? taskKey, StageChangeRequest request, string actor)
    {
        var key = Todo.NormalizeKey(taskKey);
        if (key.Length == 0) return Task.FromResult(TodoOpResult.Invalid("taskKey 不能为空"));
        return ChangeStageInternalAsync(() => Todo.FindByKey(key), 0, request, actor);
    }

    /// <summary>
    /// 状态流转（id 与 taskKey 两条入口共用，避免两套校验各写一份再漂移）。
    /// Blocked 必须带原因（否则台账里只有"阻塞"两个字，下一步无人能接手）。
    /// </summary>
    private async Task<TodoOpResult> ChangeStageInternalAsync(Func<Todo?> loader, int id, StageChangeRequest request, string actor)
    {
        if (request == null) return TodoOpResult.Invalid("请求体不能为空");
        if (!TodoStage.TryParse(request.Stage, out var target))
            return TodoOpResult.Invalid($"stage 无效：{request.Stage}。可用值：{string.Join("/", TodoStage.All.Select(TodoStage.ToName))}");

        var todo = loader();
        if (todo == null) return TodoOpResult.NotFound(id > 0 ? $"待办 {id} 不存在" : "任务不存在");

        var from = todo.Stage;
        if (!TodoStage.CanTransit(from, target))
            return TodoOpResult.Conflict($"非法状态流转：{TodoStage.ToName(from)} → {TodoStage.ToName(target)}。{TodoStage.DescribeAllowed(from)}");

        if (target == TodoStage.Blocked && string.IsNullOrWhiteSpace(request.BlockReason))
            return TodoOpResult.Invalid("流转到 Blocked 必须给出 reason/blockReason（阻塞在哪、缺什么）");

        todo.Stage = target;
        todo.Status = TodoStage.ToLegacyStatus(target);
        todo.CompletedAt = target switch
        {
            TodoStage.Done => todo.CompletedAt == DateTime.MinValue ? DateTime.Now : todo.CompletedAt,
            TodoStage.Draft => DateTime.MinValue,
            _ => todo.CompletedAt
        };
        todo.UpdatedAt = DateTime.Now;
        await todo.UpdateAsync();

        // 留痕：状态变化必须留下一条记录（AGENTS §10：结论要能追溯到实际发生）
        await _records.AppendSystemAsync(todo.Id, actor,
            $"状态流转：{TodoStage.ToName(from)} → {TodoStage.ToName(target)}",
            detail: string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason!.Trim(),
            blockReason: target == TodoStage.Blocked ? request.BlockReason : null,
            stageFrom: from, stageTo: target);

        var dto = TodoProjection.ToDto(todo, ProjectName(todo.ProjectId));
        return TodoOpResult.Success(dto);
    }

    public async Task<TodoOpResult> LinkProjectAsync(int id, LinkProjectRequest request)
    {
        var todo = Todo.FindById(id);
        if (todo == null) return TodoOpResult.NotFound($"待办 {id} 不存在");
        if (request == null) return TodoOpResult.Invalid("请求体不能为空");

        var path = (request.Path ?? string.Empty).Trim();
        if (path.Length == 0 && request.ProjectId is > 0)
        {
            var known = _projects.Get(request.ProjectId.Value);
            if (known == null) return TodoOpResult.NotFound($"项目 {request.ProjectId} 不存在");
            path = known.Root;
        }
        if (path.Length == 0) return TodoOpResult.Invalid("请给出项目路径（path）或项目 Id（projectId）");

        // 关联是显式动作 ⇒ 允许登记宿主项目档案（否则宿主里没有该目录时 ProjectId 落成 0，
        // 项目过滤/计数/归并全失效，详情面板还会显示"未关联项目"）
        var applied = ApplyProjectFields(todo, path, request.ProjectId, registerIfMissing: true);
        if (!applied.Ok) return applied;

        await todo.UpdateAsync();
        await _records.AppendSystemAsync(todo.Id, "manual", "关联项目",
            detail: $"原始写法：{path}", result: $"项目根：{todo.ProjectRoot}（#{todo.ProjectId}）");

        return TodoOpResult.Success(TodoProjection.ToDto(todo, ProjectName(todo.ProjectId)));
    }

    public async Task<TodoOpResult> UnlinkProjectAsync(int id)
    {
        var todo = Todo.FindById(id);
        if (todo == null) return TodoOpResult.NotFound($"待办 {id} 不存在");

        var previous = todo.ProjectRoot;
        todo.ProjectId = 0;
        todo.ProjectRoot = string.Empty;
        todo.ProjectPathRaw = string.Empty;
        todo.UpdatedAt = DateTime.Now;
        await todo.UpdateAsync();

        await _records.AppendSystemAsync(todo.Id, "manual", "解除项目关联", detail: $"原项目根：{previous}");
        return TodoOpResult.Success(TodoProjection.ToDto(todo));
    }

    /// <summary>
    /// 历史行回填（幂等，只改需要改的行）：
    /// ① 补 <c>TaskKey</c>（扩列前的行为空串，agent 无法按 key 引用）；
    /// ② <c>Status=Completed</c> 的行把 <c>Stage</c> 落回 Done —— 否则老完成的待办在新状态机上会显示成 Draft，
    ///    与它自己的 Status=1 自相矛盾。
    /// </summary>
    public Task<int> BackfillLegacyRowsAsync() => Task.FromResult(TodoBackfill.Run());

    /// <summary>
    /// 解析路径并写三个投影字段。
    ///
    /// <paramref name="registerIfMissing"/> 必须区分两种入口（2026-10-07 走查实测缺陷）：
    /// 建单时随手写个路径 ⇒ <b>false</b>（不污染宿主项目清单，此时 ProjectId 允许为 0）；
    /// 用户点「关联项目」是<b>显式动作</b> ⇒ <b>true</b>，否则宿主里没有档案时 ProjectId 落成 0，
    /// 于是"列表显示已关联、详情显示未关联"，且项目过滤/项目计数/按项目归并全部失效
    /// （"一致的路径认为是同一个项目"这条要求在数据层就没落地）。
    /// </summary>
    private TodoOpResult ApplyProjectFields(Todo todo, string? path, int? projectId, bool registerIfMissing)
    {
        var text = (path ?? string.Empty).Trim();
        if (text.Length == 0 && projectId is > 0)
        {
            var known = _projects.Get(projectId.Value);
            if (known == null) return TodoOpResult.NotFound($"项目 {projectId} 不存在");
            text = known.Root;
        }
        if (text.Length == 0)
        {
            if (projectId is > 0) return TodoOpResult.NotFound($"项目 {projectId} 不存在");
            return TodoOpResult.Success();   // 两者都没给：不关联，正常继续
        }

        var resolved = _projects.Resolve(text, registerIfMissing);
        if (!resolved.Ok) return TodoOpResult.Invalid(resolved.Error ?? "项目路径无法解析");

        if (projectId is > 0 && resolved.ProjectId > 0 && projectId.Value != resolved.ProjectId)
            return TodoOpResult.Invalid($"path 指向项目 #{resolved.ProjectId}，与 projectId={projectId} 不一致");

        todo.ProjectId = resolved.ProjectId;
        todo.ProjectRoot = resolved.Root;
        todo.ProjectPathRaw = text;
        return TodoOpResult.Success();
    }

    private string? ProjectName(int projectId) => projectId > 0 ? _projects.NamesFor([projectId]).Pick(projectId) : null;

    private TodoDto Project(Todo todo) => TodoProjection.ToDto(
        todo,
        ProjectName(todo.ProjectId),
        TaskExecution.CountByTasks([todo.Id]).Pick(todo.Id));

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("标题不能为空", nameof(title));
        if (title.Length > MaxTitleLength) throw new ArgumentException($"标题长度不能超过 {MaxTitleLength} 字符", nameof(title));
    }

    private static void ValidateRemark(string? remark)
    {
        if (remark != null && remark.Length > MaxRemarkLength)
            throw new ArgumentException($"备注长度不能超过 {MaxRemarkLength} 字符", nameof(remark));
    }
}
