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
        return Ok(list.Select(ToApi).ToList());
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
        return Ok(ToApi(dto));
    }

    private static ApprovalApiItem ToApi(ApprovalDto d) => new()
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
    }
}
