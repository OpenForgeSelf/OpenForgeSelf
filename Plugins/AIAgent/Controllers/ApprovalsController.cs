using System.Text.Json.Serialization;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Controllers;

/// <summary>
/// 智能体工具审批 API（P0）：列表 + 决策。路径与前端对齐：<c>/api/ai-agent/approvals</c>。
/// </summary>
[ApiController]
[Route("api/ai-agent/approvals")]
public sealed class ApprovalsController : ControllerBase
{
    private readonly IContext _ctx;

    /// <summary>
    /// 插件子容器不含宿主契约。审批服务经 IContext 在请求时取，避免构造注入 500。
    /// </summary>
    public ApprovalsController(IContext ctx)
    {
        _ctx = ctx;
    }

    private ActionResult? MissingApprovalService()
        => _ctx.Get<IApprovalService>() == null
            ? StatusCode(503, new { error = "宿主未提供审批服务" })
            : null;

    /// <summary>GET /api/ai-agent/approvals?sessionId=</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApprovalApiItem>>> List([FromQuery] string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest(new { error = "sessionId 必填" });
        if (MissingApprovalService() is { } missingList)
            return missingList;

        var list = await _ctx.Get<IApprovalService>()!.ListBySessionAsync(sessionId, ct).ConfigureAwait(false);
        return Ok(list.Select(d => ToApi(d)).ToList());
    }

    /// <summary>POST /api/ai-agent/approvals/{id}/decide  body { approved, note? }</summary>
    [HttpPost("{id:long}/decide")]
    public async Task<ActionResult<ApprovalApiItem>> Decide(long id, [FromBody] DecideBody body, CancellationToken ct)
    {
        if (body == null)
            return BadRequest(new { error = "body 必填" });
        if (MissingApprovalService() is { } missingDecide)
            return missingDecide;

        var dto = await _ctx.Get<IApprovalService>()!.DecideAsync(id, body.Approved, body.Note, ct).ConfigureAwait(false);
        if (dto == null)
            return NotFound(new { error = $"审批 {id} 不存在" });

        XTrace.Log.Info("[ApprovalsController] decide id={0} approved={1}", id, body.Approved);

        // P2②（B4）：审批挂起时回合已收束（Suspended），若决策后无人触发第二轮，
        // 用户批准/拒绝后代理永远挂着。决策写库成功即尝试唤醒该会话代理续跑。
        var (resumed, resumeNote) = await TryWakeAgentAsync(dto.SessionId, body.Approved).ConfigureAwait(false);
        XTrace.Log.Info("[ApprovalsController] wake sessionId={0} resumed={1} note={2}", dto.SessionId, resumed, resumeNote);

        return Ok(ToApi(dto, resumed, resumeNote));
    }

    /// <summary>
    /// P2② 决策后唤醒：代理存在且空闲 ⇒ Inbox.Followup 注入唤醒输入 + 后台 RunAsync
    /// （回合启动即 <see cref="IApprovalService.ConsumeContinuationsAsync"/> 取已决策审批续跑：
    /// 批准解包 <c>ApprovalRequiredAIFunction</c> 原参数执行；拒绝喂回失败结果零执行）。
    /// 不唤醒面：宿主无 IAgentRegistry / 会话无代理实例（下次对话回合启动同样消费续跑）/
    /// 代理运行中（并发守卫，续跑由其下一回合启动消费）。
    /// </summary>
    private async Task<(bool Resumed, string? ResumeNote)> TryWakeAgentAsync(string sessionId, bool approved)
    {
        var registry = _ctx.Get<IAgentRegistry>();
        if (registry == null)
            return (false, "宿主未提供 IAgentRegistry 契约，无法自动唤醒");

        IAgent? agent;
        try
        {
            agent = await registry.TryGetAsync(sessionId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ApprovalsController] wake query failed sessionId={0}: {1}", sessionId, ex);
            return (false, $"查询会话代理失败：{ex.Message}");
        }

        if (agent == null)
            return (false, "会话尚未发起过对话（无代理实例），待用户下次对话时审批续跑自动生效");

        if (agent.Status == AgentStatus.Running)
            return (false, "代理回合运行中，审批结果将在其下一回合自动生效");

        try
        {
            agent.Inbox.Followup(sessionId, approved ? "（审批已通过，请继续执行原任务）" : "（审批已拒绝，请调整方案后继续）");
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ApprovalsController] wake followup failed sessionId={0}: {1}", sessionId, ex);
            return (false, $"唤醒输入注入失败：{ex.Message}");
        }

        // 火后不管的后台回合：排空 frames 让回合自然收束；并发竞争由
        // MafTaskLoopAgent.RunAsync 的 Running 守卫兜底（异常仅记日志，不影响本次决策响应）。
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var frame in agent.RunAsync().ConfigureAwait(false))
                {
                    XTrace.Log.Debug("[ApprovalsController] wake frame sessionId={0} type={1}", sessionId, frame.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[ApprovalsController] wake run failed sessionId={0}: {1}", sessionId, ex);
            }
        });

        return (true, null);
    }

    private static ApprovalApiItem ToApi(ApprovalDto d, bool resumed = false, string? resumeNote = null) => new()
    {
        Id = d.Id,
        SessionId = d.SessionId,
        ToolName = d.ToolName,
        ArgsJson = d.ArgsJson,
        ArgsHash = d.ArgsHash,
        RiskLevel = d.RiskLevel,
        Description = d.Description,
        Status = d.Status,
        CreatedAt = d.CreatedAt,
        ExpiresAt = d.ExpiresAt,
        Resumed = resumed,
        ResumeNote = resumeNote,
    };

    /// <summary>决策请求体。</summary>
    public sealed class DecideBody
    {
        [JsonPropertyName("approved")]
        public bool Approved { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    /// <summary>camelCase 响应项（契约固定字段）。</summary>
    public sealed class ApprovalApiItem
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("sessionId")]
        public string SessionId { get; init; } = string.Empty;

        [JsonPropertyName("toolName")]
        public string ToolName { get; init; } = string.Empty;

        [JsonPropertyName("argsJson")]
        public string ArgsJson { get; init; } = "{}";

        [JsonPropertyName("argsHash")]
        public string ArgsHash { get; init; } = string.Empty;

        [JsonPropertyName("riskLevel")]
        public string RiskLevel { get; init; } = "high";

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; init; } = "Pending";

        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("expiresAt")]
        public DateTimeOffset ExpiresAt { get; init; }

        /// <summary>决策后是否已自动唤醒该会话代理续跑（仅 Decide 响应携带；List 恒 false）。</summary>
        [JsonPropertyName("resumed")]
        public bool Resumed { get; init; }

        /// <summary>未唤醒时的原因说明（Resumed=false 时非空；List 恒 null）。</summary>
        [JsonPropertyName("resumeNote")]
        public string? ResumeNote { get; init; }
    }
}
