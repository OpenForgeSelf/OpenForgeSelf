using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Controllers;

/// <summary>
/// 跨入口任务总览控制器（战役 2026-11-02-maf-runtime-hardening B3）。
/// </summary>
/// <remarks>
/// 汇聚 MAF Run 的三个入口中本插件可及的两类（架构真源 §2/§4）：
/// ① 开放任务会话 Run：<see cref="ISessionStore"/> turn 事件投影（开放任务不写 AgentRun）；
/// ② 计划任务 AgentRun：<see cref="IRunOrchestratorService.ListRunsAsync"/> 既有数据
///    （含 Scheduler 经 <c>IScheduledAgentRunTrigger</c> 触发的 Run，SessionId=scheduler:{taskId}）。
/// 第三类（Scheduler 任务列表）读侧契约不在 Abstractions，且插件间禁止引用对方程序集/HTTP 自调——
/// 由宿主前端服务层聚合既有 GET /api/scheduler/tasks
/// （docs/ai/pilot/2026-11-02-maf-runtime-hardening/02-b3-taskcenter/02-spec.md §3.5 决策 D1）。
/// 鉴权：铁律 17（ImGateway PILOT-078 同例）——管理面端点一律 ApiKeyPolicy。
/// </remarks>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/ai-agent/tasks-overview")]
public sealed class TasksOverviewController : ControllerBase
{
    /// <summary>来源标识：开放任务会话 Run。</summary>
    public const string SourceOpen = "open";

    /// <summary>来源标识：计划任务 AgentRun。</summary>
    public const string SourcePlanned = "planned";

    private readonly IPluginMessageService _messageService;
    private readonly IRunOrchestratorService _orchestrator;
    private readonly IContext _ctx;
    private ISessionStore? _sessionStore;

    /// <summary>
    /// 只注入插件自有服务（消息投影/编排器）与 <see cref="IContext"/>。
    /// <see cref="ISessionStore"/> 是宿主契约，插件子容器不含——构造注入会 500（铁律 12c，AIChatController 同例），
    /// 运行期经 <c>_ctx.Get</c> 解析。
    /// </summary>
    public TasksOverviewController(IPluginMessageService messageService, IRunOrchestratorService orchestrator, IContext ctx)
    {
        _messageService = messageService;
        _orchestrator = orchestrator;
        _ctx = ctx;
    }

    /// <summary>宿主会话事件日志（运行期经 IContext 解析，见构造函数说明）。</summary>
    private ISessionStore? SessionStore => _sessionStore ??= _ctx.Get<ISessionStore>();

    /// <summary>
    /// 跨入口任务总览：开放任务会话 Run（SessionEvent 投影）+ 计划任务 AgentRun，统一 DTO。
    /// </summary>
    /// <param name="source">来源过滤：open / planned，空=全部；其他值 400。</param>
    /// <param name="limit">开放任务会话数上限（按最后消息时间倒序截取），钳 1–100。</param>
    /// <param name="ct">取消令牌</param>
    /// <response code="200">总览响应（单桶失败不影响另一桶，错误记入 sources[].error）</response>
    /// <response code="400">source 非法</response>
    [HttpGet]
    public async Task<ActionResult<TasksOverviewResponse>> GetOverview(
        [FromQuery] string? source = null,
        [FromQuery] int limit = 30,
        CancellationToken ct = default)
    {
        var wantOpen = true;
        var wantPlanned = true;
        var src = (source ?? string.Empty).Trim().ToLowerInvariant();
        if (src is not ("" or SourceOpen or SourcePlanned))
            return BadRequest(new { error = "source 仅支持 open / planned，空=全部" });
        if (src == SourceOpen)
            wantPlanned = false;
        if (src == SourcePlanned)
            wantOpen = false;
        limit = Math.Clamp(limit, 1, 100);

        var response = new TasksOverviewResponse { GeneratedAt = DateTimeOffset.Now };

        if (wantOpen)
        {
            try
            {
                var items = await ProjectOpenTasksAsync(limit);
                if (items == null)
                {
                    // 宿主未接会话日志：桶级诚实降级（明确报因），不冒充「空数据」
                    response.Sources.Add(TaskSourceSummary.CreateFail(SourceOpen, "宿主未提供 ISessionStore"));
                }
                else
                {
                    response.Items.AddRange(items);
                    response.Sources.Add(TaskSourceSummary.CreateOk(SourceOpen, items.Count));
                }
            }
            catch (Exception ex)
            {
                // 单桶失败不拖垮总览：错误原文入 sources，前端按桶降级展示
                XTrace.Log.Error("[TasksOverview] 开放任务桶投影失败: {0}", ex.Message);
                response.Sources.Add(TaskSourceSummary.CreateFail(SourceOpen, ex.Message));
            }
        }

        if (wantPlanned)
        {
            try
            {
                var runs = await _orchestrator.ListRunsAsync(sessionId: null, status: null, page: 1, pageSize: 50);
                var items = runs.Items.Select(ProjectPlannedRun).ToList();
                response.Items.AddRange(items);
                response.Sources.Add(TaskSourceSummary.CreateOk(SourcePlanned, items.Count));
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[TasksOverview] 计划任务桶投影失败: {0}", ex.Message);
                response.Sources.Add(TaskSourceSummary.CreateFail(SourcePlanned, ex.Message));
            }
        }

        response.Success = response.Sources.Count > 0 && response.Sources.Any(s => s.Ok);
        return Ok(response);
    }

    // ---- 来源②：计划任务 AgentRun ----

    /// <summary>AgentRun 列表行 → 统一 DTO（失败原因=StuckReason，Failed/Stuck 可续跑）。</summary>
    internal static TaskOverviewDto ProjectPlannedRun(AgentRunDto run)
    {
        var status = run.Status switch
        {
            AgentRunStatus.Pending => TaskOverviewStatus.Pending,
            AgentRunStatus.Planning => TaskOverviewStatus.Planning,
            AgentRunStatus.Running => TaskOverviewStatus.Running,
            AgentRunStatus.Completed => TaskOverviewStatus.Completed,
            AgentRunStatus.Stuck => TaskOverviewStatus.Stuck,
            AgentRunStatus.Failed => TaskOverviewStatus.Failed,
            AgentRunStatus.Cancelled => TaskOverviewStatus.Cancelled,
            _ => TaskOverviewStatus.Unknown,
        };
        var retryable = run.Status is AgentRunStatus.Stuck or AgentRunStatus.Failed;
        // 失败原因：Stuck 态=卡住原因；Failed 态=错误原文（编排器把 ErrorMessage 落 StuckReason，见 RunOrchestratorService）
        var reason = run.Status switch
        {
            AgentRunStatus.Stuck => string.IsNullOrWhiteSpace(run.StuckReason) ? "等待人工介入" : run.StuckReason,
            AgentRunStatus.Failed => string.IsNullOrWhiteSpace(run.StuckReason) ? "无失败详情" : run.StuckReason,
            _ => null,
        };
        var terminal = run.Status
            is AgentRunStatus.Completed or AgentRunStatus.Failed
                or AgentRunStatus.Cancelled or AgentRunStatus.Stuck;

        return new TaskOverviewDto
        {
            Id = $"{SourcePlanned}:{run.Id}",
            Source = SourcePlanned,
            Title = FirstLine(run.TaskInput),
            Status = status,
            FailureReason = reason,
            StartedAt = ToOffset(run.CreateTime),
            EndedAt = terminal ? ToOffset(run.UpdateTime) : null,
            Retryable = retryable,
            RetryUrl = retryable ? $"/api/ai-agent/runs/{run.Id}/resume" : null,
            // T14⑥：非终态均可取消（RunStateMachine：Pending/Planning/Running/Stuck/Failed → Cancelled 允许）
            CancelUrl = run.Status is not (AgentRunStatus.Completed or AgentRunStatus.Cancelled)
                ? $"/api/ai-agent/runs/{run.Id}/cancel"
                : null,
            SourceRef = run.Id.ToString(),
        };
    }

    // ---- 来源①：开放任务会话 Run（SessionEvent 投影） ----

    /// <summary>
    /// 按最后消息时间倒序取最近 <paramref name="limit"/> 个活跃会话，逐会话投影 turn 事件为统一 DTO。
    /// 会话存储缺席（如测试装配/宿主未接）时返回 null（调用方按桶级失败降级并报因），而非 500。
    /// </summary>
    private async Task<List<TaskOverviewDto>?> ProjectOpenTasksAsync(int limit)
    {
        var store = SessionStore;
        if (store == null)
        {
            XTrace.Log.Warn("[TasksOverview] 宿主未提供 ISessionStore，开放任务桶降级为失败态");
            return null;
        }

        var sessions = await _messageService.GetSessionsAsync(SessionArchivedFilter.Active);
        var items = new List<TaskOverviewDto>(Math.Min(limit, sessions.Count));
        foreach (var session in sessions
            .OrderByDescending(s => s.LastTime)
            .Take(limit))
        {
            IReadOnlyList<SessionEvent> events;
            try
            {
                events = store.Replay(session.SessionId) ?? Array.Empty<SessionEvent>();
            }
            catch (Exception ex)
            {
                // 单会话读失败只降级该行，不拖垮整桶
                XTrace.Log.Error("[TasksOverview] 会话 {0} 事件回放失败: {1}", session.SessionId, ex.Message);
                events = Array.Empty<SessionEvent>();
            }
            items.Add(ProjectOpenSession(session, events));
        }
        return items;
    }

    /// <summary>会话事件序列 → 统一 DTO（投影规则见 02-spec §3.3 表）。</summary>
    internal static TaskOverviewDto ProjectOpenSession(SessionSummaryModel session, IReadOnlyList<SessionEvent> events)
    {
        // 一次线性扫描：最后一个 turn/start、最后一个 turn/end
        var lastStartIdx = -1;
        var lastEndIdx = -1;
        TurnEndReason? lastEndReason = null;
        for (var i = 0; i < events.Count; i++)
        {
            switch (events[i])
            {
                case TurnStartEvent:
                    lastStartIdx = i;
                    break;
                case TurnEndEvent end:
                    lastEndIdx = i;
                    lastEndReason = end.Reason;
                    break;
            }
        }

        // 回合在途：有 turn/start 且其后没有 turn/end（最后事件序即当前回合）
        var turnInFlight = lastStartIdx >= 0 && lastStartIdx > lastEndIdx;

        // 未闭合审批：最后一个 tool/approval-request 之后没有同 CallId 的 tool/result
        var approvalTool = FindUnresolvedApprovalTool(events);

        // 末回合（已收束）内的失败痕迹：assistant/attempt 的 ErrorKind（aborted=用户中止，其余=失败）
        var attemptKind = FindAttemptKind(events, lastStartIdx, lastEndIdx);

        string status;
        string? reason = null;
        var retryable = false;
        if (approvalTool != null)
        {
            status = TaskOverviewStatus.Stuck;
            reason = $"等待工具审批：{approvalTool}";
            retryable = true;
        }
        else if (turnInFlight)
        {
            status = TaskOverviewStatus.Running;
        }
        else if (lastEndIdx >= 0)
        {
            switch (lastEndReason)
            {
                case TurnEndReason.Completed:
                    status = TaskOverviewStatus.Completed;
                    break;
                case TurnEndReason.Suspended:
                    status = TaskOverviewStatus.Stuck;
                    reason = "回合暂停（可续跑）";
                    retryable = true;
                    break;
                case TurnEndReason.MaxTokens:
                    status = TaskOverviewStatus.Stuck;
                    reason = "回合因 token 上限收束";
                    retryable = true;
                    break;
                case TurnEndReason.Aborted:
                    // attempt 痕迹区分「用户中止」与「失败」；无痕迹按用户中止处理（不强判）
                    if (attemptKind != null && attemptKind != "aborted")
                    {
                        status = TaskOverviewStatus.Failed;
                        reason = $"执行失败（{attemptKind}）";
                        retryable = true;
                    }
                    else
                    {
                        status = TaskOverviewStatus.Cancelled;
                    }
                    break;
                case TurnEndReason.NoInput:
                    status = TaskOverviewStatus.Completed;
                    break;
                default:
                    status = TaskOverviewStatus.Unknown;
                    break;
            }
        }
        else
        {
            // 有会话但无任何 turn 事件（历史会话/旧数据）：不假判，标 unknown
            status = TaskOverviewStatus.Unknown;
        }

        var startedAt = events.OfType<TurnStartEvent>().FirstOrDefault()?.Timestamp
                        ?? new DateTimeOffset(session.LastTime);
        var endedAt = lastEndIdx >= 0 && events[lastEndIdx] is TurnEndEvent te ? (DateTimeOffset?)te.Timestamp : null;

        return new TaskOverviewDto
        {
            Id = $"{SourceOpen}:{session.SessionId}",
            Source = SourceOpen,
            Title = string.IsNullOrWhiteSpace(session.Title) ? "未命名会话" : session.Title,
            Status = status,
            FailureReason = reason,
            StartedAt = startedAt,
            EndedAt = endedAt,
            Retryable = retryable,
            SourceRef = session.SessionId,
        };
    }

    /// <summary>未闭合审批工具名：最后一个 approval-request 之后无同 CallId 的 tool/result 则未闭合。</summary>
    private static string? FindUnresolvedApprovalTool(IReadOnlyList<SessionEvent> events)
    {
        for (var i = events.Count - 1; i >= 0; i--)
        {
            if (events[i] is not ToolApprovalRequestEvent req)
                continue;
            for (var j = i + 1; j < events.Count; j++)
            {
                if (events[j] is ToolResultEvent res && res.CallId == req.CallId)
                    return null; // 已闭合（其后同 CallId 有结果）
            }
            return req.ToolName;
        }
        return null;
    }

    /// <summary>末回合（start 与 end 之间）的 assistant/attempt ErrorKind；无回合界或无痕迹返回 null。</summary>
    private static string? FindAttemptKind(IReadOnlyList<SessionEvent> events, int startIdx, int endIdx)
    {
        if (startIdx < 0 || endIdx <= startIdx)
            return null;
        for (var i = endIdx - 1; i > startIdx; i--)
        {
            if (events[i] is AssistantAttemptEvent attempt && !string.IsNullOrWhiteSpace(attempt.ErrorKind))
                return attempt.ErrorKind;
        }
        return null;
    }

    /// <summary>标题取任务原文首行，超 60 字截断加省略号。</summary>
    private static string FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "（无任务原文）";
        var line = text.Split('\n', 2)[0].Trim();
        return line.Length <= 60 ? line : line[..60] + "…";
    }

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue ? new DateTimeOffset(value.Value) : null;
}

/// <summary>统一状态字典（02-spec §3.2；scheduled 由前端 Scheduler 桶使用，后端不出）。</summary>
public static class TaskOverviewStatus
{
    public const string Running = "running";
    public const string Planning = "planning";
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Stuck = "stuck";
    public const string Cancelled = "cancelled";
    public const string Unknown = "unknown";
}

/// <summary>跨入口任务统一 DTO（来源/标题/状态/起止时间/失败原因/可重试标记，02-spec §3.1）。</summary>
public class TaskOverviewDto
{
    /// <summary>稳定标识：open:{sessionId} / plan:{runId}。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>来源：open（开放任务会话 Run）/ planned（计划任务 AgentRun）。</summary>
    public string Source { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>归一状态（<see cref="TaskOverviewStatus"/>）。</summary>
    public string Status { get; set; } = TaskOverviewStatus.Unknown;

    /// <summary>失败/卡住原因原文（可空）。</summary>
    public string? FailureReason { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>是否可重试/续跑（failed/stuck）。</summary>
    public bool Retryable { get; set; }

    /// <summary>重试端点（planned：POST /api/ai-agent/runs/{id}/resume，SSE 流）。</summary>
    public string? RetryUrl { get; set; }

    /// <summary>取消地址（非终态可取消 → /api/ai-agent/runs/{id}/cancel；T14⑥）。</summary>
    public string? CancelUrl { get; set; }

    /// <summary>源标识原值（sessionId / runId）。</summary>
    public string SourceRef { get; set; } = string.Empty;
}

/// <summary>单桶汇聚结果（桶级降级：失败不拖垮总览）。</summary>
public class TaskSourceSummary
{
    public string Source { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool Ok { get; set; }
    public string? Error { get; set; }

    public static TaskSourceSummary CreateOk(string source, int count) => new() { Source = source, Count = count, Ok = true };
    public static TaskSourceSummary CreateFail(string source, string error) => new() { Source = source, Ok = false, Error = error };
}

/// <summary>总览响应（camelCase 序列化由 ASP.NET Core 全局约定保证）。</summary>
public class TasksOverviewResponse
{
    public bool Success { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public List<TaskSourceSummary> Sources { get; set; } = new();
    public List<TaskOverviewDto> Items { get; set; } = new();
}
