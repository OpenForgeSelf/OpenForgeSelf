using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Entities;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using static ForgeSelf.Api.Plugins.TodoTracker.Entities.Todo;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 下发与委派实现（PILOT-054 · FR-1/FR-5/FR-6）。
///
/// 三条不显然的口径：
/// <list type="bullet">
/// <item><b>下发必填</b>由 <see cref="DispatchPayloadBuilder.Missing"/> 单点判定，服务不再自己列一遍
/// （否则"预览说能发、真发被拒"这类分裂迟早出现）。</item>
/// <item><b>一键执行走能力接缝</b> <see cref="IAgentTaskGateway"/>，缺席即 503 原文，
/// 绝不静默成功，也不绕去直连 AgentHub 的 HTTP（architecture-design 铁律 2/3）。</item>
/// <item><b>每一次状态挪动都留一条执行记录</b>：下发、领取、委派、回写各有自己的痕，
/// 台账才拼得出"谁在什么时候把它推到哪一步"。</item>
/// </list>
/// </summary>
public class TodoDispatchService : ITodoDispatchService
{
    /// <summary>结果摘要进台账的长度上限（ResultText 可能很长，台账要的是要点不是全文）。</summary>
    internal const int MaxResultSummary = 2000;

    private readonly ITaskExecutionService _records;
    private readonly IAgentTaskGateway _gateway;
    private readonly ITodoProjectService _projects;

    public TodoDispatchService(ITaskExecutionService records, IAgentTaskGateway gateway, ITodoProjectService projects)
    {
        _records = records;
        _gateway = gateway;
        _projects = projects;
    }

    /// <inheritdoc />
    public Task<DispatchPreviewDto?> PreviewAsync(int id, string? baseUrl) => PreviewInternalAsync(FindById(id), baseUrl);

    /// <inheritdoc />
    public Task<DispatchPreviewDto?> PreviewByKeyAsync(string? taskKey, string? baseUrl) =>
        PreviewInternalAsync(FindByKey(taskKey), baseUrl);

    /// <inheritdoc />
    public async Task<TodoOpResult> DispatchAsync(int id, string? assignee, string actor)
    {
        var todo = FindById(id);
        if (todo == null) return TodoOpResult.NotFound($"待办 {id} 不存在");

        var payload = TodoProjection.ToPayload(todo);
        var missing = DispatchPayloadBuilder.Missing(payload);
        if (missing.Count > 0)
            return TodoOpResult.Invalid($"下发前还缺：{string.Join("、", missing)}（目标/正文/验收判据/验证命令缺一不可）");

        var from = todo.Stage;
        var steps = new List<(int From, int To)>();

        // Draft 直接下发：先补一跳 Ready，让台账里留下"就绪"这一格，而不是凭空从草稿跳到已下发
        if (from == TodoStage.Draft)
        {
            steps.Add((TodoStage.Draft, TodoStage.Ready));
            from = TodoStage.Ready;
        }

        if (!TodoStage.CanTransit(from, TodoStage.Dispatched))
            return TodoOpResult.Conflict($"无法下发：当前是 {TodoStage.ToName(todo.Stage)}（{TodoStage.ToLabel(todo.Stage)}）。{TodoStage.DescribeAllowed(from)}");

        steps.Add((from, TodoStage.Dispatched));

        var name = (assignee ?? string.Empty).Trim();
        if (name.Length > 0) todo.Assignee = name;

        todo.Stage = TodoStage.Dispatched;
        if (todo.DispatchedAt == DateTime.MinValue) todo.DispatchedAt = DateTime.Now;
        todo.UpdatedAt = DateTime.Now;
        await todo.UpdateAsync();

        foreach (var (f, t) in steps)
        {
            await _records.AppendSystemAsync(todo.Id, actor,
                t == TodoStage.Dispatched ? "下发任务" : "状态流转：Draft → Ready",
                detail: t == TodoStage.Dispatched ? $"下发对象：{OrNone(todo.Assignee, "未指定")}" : "必填齐备，自动补齐就绪态",
                result: t == TodoStage.Dispatched ? $"验证命令 {DispatchPayloadBuilder.SplitLines(todo.Verification).Count} 条、判据 {DispatchPayloadBuilder.SplitLines(todo.Acceptance).Count} 条" : null,
                stageFrom: f, stageTo: t);
        }

        XTrace.Log.Info("[todo-tracker] 任务已下发：id={0} key={1} assignee={2}", todo.Id, todo.TaskKey, todo.Assignee);
        return TodoOpResult.Success(Project(todo));
    }

    /// <inheritdoc />
    public async Task<DelegateToAgentResultDto> DelegateAsync(int id, DelegateToAgentRequest? request, string actor, string? baseUrl = null)
    {
        var todo = FindById(id);
        if (todo == null) return FailedDelegate($"待办 {id} 不存在");

        var payload = TodoProjection.ToPayload(todo);
        var missing = DispatchPayloadBuilder.Missing(payload);
        if (missing.Count > 0)
            return FailedDelegate($"下发前还缺：{string.Join("、", missing)}");

        var mode = (request?.PermissionMode ?? todo.PermissionMode).IsNullOrEmpty() ? "read-only" : request!.PermissionMode ?? todo.PermissionMode;
        if (ForbiddenModes.Contains(mode.Trim().ToLowerInvariant()))
            return FailedDelegate($"权限模式「{mode}」被禁止（不得使用无限权限模式）");

        if (request?.AgentId is > 0) todo.AgentId = request.AgentId.Value;
        todo.PermissionMode = mode;

        var prompt = DispatchPayloadBuilder.BuildMarkdown(payload, baseUrl);
        var outcome = await _gateway.SubmitAsync(new AgentDelegationRequest
        {
            Prompt = prompt,
            AgentId = request?.AgentId is > 0 ? request.AgentId : (todo.AgentId > 0 ? todo.AgentId : null),
            Cwd = todo.ProjectRoot.IsNullOrEmpty() ? null : todo.ProjectRoot,
            PermissionMode = mode,
            CreatedBy = "todo-tracker"
        });

        if (!outcome.Success)
        {
            // 失败不改状态、不写成功痕：任务停在哪还看得见
            await _records.AppendSystemAsync(todo.Id, actor, "一键交给 AgentHub 执行",
                result: $"失败：{outcome.Error}", risks: outcome.SeamMissing ? "委派能力缺席" : null);
            return new DelegateToAgentResultDto { Ok = false, Error = outcome.Error ?? "委派失败", SeamMissing = outcome.SeamMissing, TodoId = todo.Id };
        }

        var value = outcome.Value!;
        var steps = await AdvanceToRunningAsync(todo, actor);

        var from = todo.Stage;
        todo.AgentTaskKey = value.TaskKey ?? string.Empty;
        todo.AgentId = value.AgentId;
        todo.Stage = TodoStage.Running;
        todo.Status = TodoStage.ToLegacyStatus(TodoStage.Running);
        if (todo.DispatchedAt == DateTime.MinValue) todo.DispatchedAt = DateTime.Now;
        todo.UpdatedAt = DateTime.Now;

        string? backfillWarning = null;
        try
        {
            await todo.UpdateAsync();
        }
        catch (Exception ex)
        {
            // 已入队是真的、回填失败也是真的：宁可让用户看见不一致，也不能吞掉任何一边
            backfillWarning = $"委派已入队（{value.TaskKey}）但台账回填失败：{ex.Message}";
            XTrace.Log.Error("[todo-tracker] {0}", backfillWarning);
        }

        await _records.AppendSystemAsync(todo.Id, actor, "一键交给 AgentHub 执行",
            detail: $"agent={value.AgentName}(#{value.AgentId}) 权限模式={mode} 工作目录={OrNone(todo.ProjectRoot, "agent 默认")}",
            result: $"已入队，状态 {value.Status}，taskKey={value.TaskKey}",
            evidence: backfillWarning == null ? $"taskKey:{value.TaskKey}" : backfillWarning,
            risks: backfillWarning,
            stageFrom: from, stageTo: TodoStage.Running);

        return new DelegateToAgentResultDto
        {
            Ok = true,
            TodoId = todo.Id,
            TaskKey = value.TaskKey ?? string.Empty,
            AgentId = value.AgentId,
            AgentName = value.AgentName ?? string.Empty,
            Status = value.Status ?? string.Empty,
            Cwd = todo.ProjectRoot,
            Backfilled = backfillWarning == null,
            BackfillWarning = backfillWarning,
            Steps = steps
        };
    }

    /// <inheritdoc />
    public async Task<AgentStatusDto> AgentStatusAsync(int id)
    {
        var todo = FindById(id);
        if (todo == null) return AgentStatusDto.Failed($"待办 {id} 不存在", 404);
        if (todo.AgentTaskKey.IsNullOrEmpty()) return AgentStatusDto.Failed("本任务还没委派给 agent", 400);

        return await ReadStatusAsync(todo);
    }

    /// <inheritdoc />
    public async Task<RecordAgentResultDto> RecordAgentResultAsync(int id, string actor)
    {
        var todo = FindById(id);
        if (todo == null) return new RecordAgentResultDto { Ok = false, Error = $"待办 {id} 不存在" };
        if (todo.AgentTaskKey.IsNullOrEmpty())
            return new RecordAgentResultDto { Ok = false, Error = "本任务还没委派给 agent" };

        var status = await ReadStatusAsync(todo);
        if (!status.Ok)
            return new RecordAgentResultDto { Ok = false, Error = status.Error ?? "读回委派状态失败" };

        var target = AgentOutcomeStage(status.Status);
        var request = new CreateTaskExecutionRequest
        {
            Actor = actor,
            Action = $"agent 执行回写：{status.Status}",
            Detail = $"taskKey={status.TaskKey} 退出码={(status.ExitCode?.ToString() ?? "未结束")} 耗时={status.ElapsedMs}ms 工作目录={OrNone(status.Cwd, "未记录")}",
            Result = status.ResultSummary,
            FilesChanged = status.FilesChanged,
            Verification = status.Verification,
            Risks = status.ErrorCode.IsNullOrEmpty() ? null : $"错误码：{status.ErrorCode}",
            Residuals = target == TodoStage.Review ? null : "agent 未判成功，遗留待人工确认",
            Evidence = $"agent-hub taskKey:{status.TaskKey}",
            ElapsedMs = status.ElapsedMs,
            // 只有终态才推状态；非终态（Queued/Running/AwaitingPermission）只记账，不假装做完了
            StageTo = target >= 0 ? TodoStage.ToName(target) : null
        };

        var appended = await _records.AppendAsync(todo.Id, request, actor);
        if (!appended.Ok)
            return new RecordAgentResultDto { Ok = false, Error = appended.Error };

        var latest = FindById(id) ?? todo;
        return new RecordAgentResultDto
        {
            Ok = true,
            // 刚写的那条的序号：直查库取最大 Seq（AppendAsync 只回任务投影，不带记录 id，这里以库为准而不是猜）
            Seq = TaskExecution.MaxSeqOf(todo.Id),
            Stage = TodoStage.ToName(latest.Stage)
        };
    }

    /// <inheritdoc />
    public async Task<TodoOpResult> ClaimNextAsync(string? assignee, int projectId, string actor)
    {
        var exp = new WhereExpression();
        exp &= _.Stage == TodoStage.Dispatched;
        var name = (assignee ?? string.Empty).Trim();
        if (name.Length > 0) exp &= _.Assignee == name;
        if (projectId > 0) exp &= _.ProjectId == projectId;

        // 优先级小的先做（P1>P2>P3），同级按创建时间
        var candidates = Todo.FindAll(exp, new PageParameter { PageSize = 20, Sort = "Priority ASC, CreatedAt ASC" });
        if (candidates.Count == 0)
            return TodoOpResult.Success(null);   // 空态不是错误：agent 据此知道"现在没有我的活"

        foreach (var candidate in candidates)
        {
            // 逐条重读再判定：列表读出来到写回之间可能已被另一个 agent 领走（并发领取只能靠"重读后仍是我的目标行"来兜）
            var todo = Todo.FindById(candidate.Id);
            if (todo == null || todo.Stage != TodoStage.Dispatched) continue;

            var from = todo.Stage;
            todo.Stage = TodoStage.Running;
            todo.Status = TodoStage.ToLegacyStatus(TodoStage.Running);
            if (name.Length > 0) todo.Assignee = name;
            if (todo.DispatchedAt == DateTime.MinValue) todo.DispatchedAt = DateTime.Now;
            todo.UpdatedAt = DateTime.Now;
            await todo.UpdateAsync();

            var payload = TodoProjection.ToPayload(todo);
            await _records.AppendSystemAsync(todo.Id, actor, "领取任务",
                detail: $"下发对象：{OrNone(todo.Assignee, "未指定")}；项目：{OrNone(todo.ProjectRoot, "未关联")}",
                result: $"验证命令 {DispatchPayloadBuilder.SplitLines(payload.Verification).Count} 条待跑",
                nextStep: "按验收判据逐条自查后回报 records",
                stageFrom: from, stageTo: TodoStage.Running);

            XTrace.Log.Info("[todo-tracker] 任务被领取：id={0} key={1} actor={2}", todo.Id, todo.TaskKey, actor);
            return TodoOpResult.Success(Project(todo));
        }

        return TodoOpResult.Success(null);
    }

    private async Task<DispatchPreviewDto?> PreviewInternalAsync(Todo? todo, string? baseUrl)
    {
        if (todo == null) return null;

        var payload = TodoProjection.ToPayload(todo);
        var prompt = DispatchPayloadBuilder.BuildMarkdown(payload, baseUrl);

        return new DispatchPreviewDto
        {
            TaskKey = todo.TaskKey,
            PromptMarkdown = prompt,
            PayloadJson = DispatchPayloadBuilder.BuildAgentHubJson(payload, todo.AgentId > 0 ? todo.AgentId : null),
            Missing = DispatchPayloadBuilder.Missing(payload).ToList(),
            Warnings = DispatchPayloadBuilder.Warnings(payload).ToList(),
            CanDispatch = DispatchPayloadBuilder.CanDispatch(payload),
            DelegationAvailable = _gateway.IsAvailable,
            DelegationError = _gateway.IsAvailable ? null : AgentTaskGateway.SeamNotAvailableMessage,
            CanDelegate = DispatchPayloadBuilder.CanDispatch(payload) && _gateway.IsAvailable,
            Agents = _gateway.AvailableAgents()
                .Select(a => new AgentOptionDto { Id = a.Id, Name = a.Name, Vendor = a.Vendor, DefaultCwd = a.DefaultCwd })
                .ToList()
        };
    }

    private async Task<List<string>> AdvanceToRunningAsync(Todo todo, string actor)
    {
        var steps = new List<string>();
        var stage = todo.Stage;

        if (stage is TodoStage.Draft or TodoStage.Ready)
        {
            if (stage == TodoStage.Draft)
            {
                await _records.AppendSystemAsync(todo.Id, actor, "状态流转：Draft → Ready", stageFrom: TodoStage.Draft, stageTo: TodoStage.Ready);
                steps.Add("Draft → Ready");
                stage = TodoStage.Ready;
            }
            await _records.AppendSystemAsync(todo.Id, actor, "状态流转：Ready → Dispatched",
                detail: "随一键执行自动补齐下发态", stageFrom: TodoStage.Ready, stageTo: TodoStage.Dispatched);
            steps.Add("Ready → Dispatched");
            stage = TodoStage.Dispatched;
        }

        if (stage != TodoStage.Running && !TodoStage.CanTransit(stage, TodoStage.Running))
            XTrace.Log.Warn("[todo-tracker] 从 {0} 无法直接进入 Running，台账按实际状态记录", TodoStage.ToName(stage));

        return steps;
    }

    private async Task<AgentStatusDto> ReadStatusAsync(Todo todo)
    {
        var result = await _gateway.Query(todo.AgentTaskKey);
        if (result.SeamMissing) return AgentStatusDto.Failed(result.Error ?? AgentTaskGateway.SeamNotAvailableMessage, 503);
        if (!result.Success || result.Value == null)
            return AgentStatusDto.Failed(result.Error ?? "委派任务不存在", 404);

        var s = result.Value;
        return new AgentStatusDto
        {
            Ok = true,
            TodoId = todo.Id,
            TaskKey = s.TaskKey,
            Status = s.Status,
            Terminal = s.Terminal,
            ExitCode = s.ExitCode,
            ErrorCode = s.ErrorCode,
            ElapsedMs = s.ElapsedMs,
            Cwd = s.Cwd,
            ResultSummary = Truncate(s.ResultText, MaxResultSummary),
            Verification = BuildVerification(s),
            FilesChanged = s.Artifacts
                .Where(a => !a.Text.IsNullOrEmpty())
                .Select(a => new ChangedFileDto { Path = a.Text!.Trim(), Change = a.Type })
                .ToList()
        };
    }

    private static string BuildVerification(AgentDelegationSnapshot snapshot) =>
        $"委派状态 {snapshot.Status}" +
        (snapshot.Terminal ? $"，退出码 {snapshot.ExitCode}" : "（未结束）") +
        $"，耗时 {snapshot.ElapsedMs}ms，产物 {snapshot.Artifacts.Count} 项";

    /// <summary>委派结果 → 本插件阶段：成功进待验收；失败/超时/中断/取消不动状态（由人判）。</summary>
    internal static int AgentOutcomeStage(string? status) => status switch
    {
        "Succeeded" => TodoStage.Review,
        _ => -1
    };

    private static string? Truncate(string? text, int max)
    {
        if (text.IsNullOrEmpty()) return null;
        var value = text!.Trim();
        return value.Length <= max ? value : value[..max] + "…（已截断）";
    }

    private static Todo? FindById(int id) => id > 0 ? Todo.FindById(id) : null;

    private static Todo? FindByKey(string? taskKey)
    {
        var key = Todo.NormalizeKey(taskKey);
        return key.Length == 0 ? null : Todo.FindByKey(key);
    }

    /// <summary>投影（写路径专用：刚留过痕，记录数当场重算，免得界面显示 0 条记录）。</summary>
    private TodoDto Project(Todo todo) => TodoProjection.ToDto(
        todo,
        todo.ProjectId > 0 ? _projects.NamesFor([todo.ProjectId]).Pick(todo.ProjectId) : null,
        TaskExecution.CountByTasks([todo.Id]).Pick(todo.Id));

    private static DelegateToAgentResultDto FailedDelegate(string error) => new() { Ok = false, Error = error };

    private static string OrNone(string? value, string fallback) => value.IsNullOrEmpty() ? fallback : value!.Trim();

    /// <summary>无限权限模式黑名单（与 AgentHub 侧 <c>AgentPolicy</c> 同口径；双层各自成立，不互相替代）。</summary>
    private static readonly HashSet<string> ForbiddenModes =
        new(StringComparer.OrdinalIgnoreCase) { "yolo", "danger-full-access", "dangerously-skip-permissions" };
}
