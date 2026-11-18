using System.Text.Json;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.AIAgent.Controllers;

/// <summary>统一 Agent 能力入口，供本页与其它插件助手面板复用。旧 /api/ai-agent/chat|runs|workflow 不要再作为主入口。</summary>
[ApiController]
[Route("api/agent")]
public sealed class AgentFacadeController : ControllerBase
{
    private static readonly JsonSerializerOptions SseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAgentFacadeService _facade;

    public AgentFacadeController(IAgentFacadeService facade) => _facade = facade;

    [HttpPost("sessions")]
    public ActionResult<AgentSessionSummaryDto> Create([FromBody] CreateAgentSessionRequest request)
    {
        try
        {
            return Ok(_facade.CreateSession(request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("sessions")]
    public ActionResult<IReadOnlyList<AgentSessionSummaryDto>> List([FromQuery] string? pluginId, [FromQuery] string? surfaceKey)
    {
        try
        {
            return Ok(_facade.ListSessions(pluginId ?? "", surfaceKey));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("sessions/{id}")]
    public ActionResult<AgentSessionDetailDto> Get(string id)
    {
        var detail = _facade.GetSession(id);
        return detail == null ? NotFound(new { error = "会话不存在" }) : Ok(detail);
    }

    [HttpPatch("sessions/{id}")]
    public ActionResult<AgentSessionSummaryDto> Patch(string id, [FromBody] PatchAgentSessionRequest request)
    {
        try
        {
            var updated = _facade.PatchSession(id, request);
            return updated == null ? NotFound(new { error = "会话不存在" }) : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("sessions/{id}")]
    public IActionResult Delete(string id) =>
        _facade.DeleteSession(id) ? NoContent() : NotFound(new { error = "会话不存在" });

    [HttpPost("sessions/{id}/messages")]
    public IActionResult PostMessage(string id, [FromBody] PostAgentMessageRequest request)
    {
        var (status, body) = _facade.PostMessage(id, request);
        return StatusCode(status, body);
    }

    [HttpGet("sessions/{id}/events")]
    public async Task Events(string id, [FromQuery] long after = 0, CancellationToken cancellationToken = default)
    {
        if (_facade.GetSession(id) == null)
        {
            Response.StatusCode = 404;
            await Response.WriteAsJsonAsync(new { error = "会话不存在" }, cancellationToken);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var cursor = after;
        foreach (var ev in _facade.ReplayEvents(id, cursor))
        {
            await WriteSse(ev, cancellationToken);
            cursor = ev.Seq;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var ev = await _facade.WaitEventAsync(id, cursor, cancellationToken);
                if (ev == null) break;
                await WriteSse(ev, cancellationToken);
                cursor = ev.Seq;
            }
        }
        catch (OperationCanceledException)
        {
            // 客户端断开
        }
    }

    [HttpPost("sessions/{id}/plan/confirm")]
    public IActionResult ConfirmPlan(string id) => Wrap(_facade.ConfirmPlan(id));

    [HttpPost("sessions/{id}/plan/revise")]
    public IActionResult RevisePlan(string id, [FromBody] RevisePlanRequest request) =>
        Wrap(_facade.RevisePlan(id, request));

    [HttpPost("sessions/{id}/approvals/{approvalId}/decide")]
    public IActionResult Decide(string id, string approvalId, [FromBody] DecideApprovalRequest request) =>
        Wrap(_facade.DecideApproval(id, approvalId, request));

    [HttpPost("sessions/{id}/runs/{runId}/stop")]
    public IActionResult Stop(string id, string runId) => Wrap(_facade.StopRun(id, runId));

    [HttpPost("sessions/{id}/runs/{runId}/intervene")]
    public IActionResult Intervene(string id, string runId, [FromBody] InterveneRunRequest request) =>
        Wrap(_facade.Intervene(id, runId, request));

    [HttpPost("sessions/{id}/workflows/{workflowId}/pause")]
    public IActionResult PauseWorkflow(string id, string workflowId) =>
        Wrap(_facade.PauseWorkflow(id, workflowId));

    [HttpPost("sessions/{id}/workflows/{workflowId}/cancel")]
    public IActionResult CancelWorkflow(string id, string workflowId) =>
        Wrap(_facade.CancelWorkflow(id, workflowId));

    [HttpGet("sessions/{id}/workspace")]
    public IActionResult Workspace(string id, [FromQuery] string? panel) =>
        Wrap(_facade.GetWorkspace(id, panel));

    [HttpPost("sessions/{id}/workspace/memory/writeback")]
    public IActionResult WriteMemory(string id, [FromBody] WorkspaceWritebackRequest request) =>
        Wrap(_facade.WriteMemory(id, request));

    [HttpPost("sessions/{id}/workspace/design/commit")]
    public IActionResult CommitDesign(string id, [FromBody] WorkspaceWritebackRequest request) =>
        Wrap(_facade.CommitDesign(id, request));

    [HttpPost("sessions/{id}/workspace/todos/{todoId}/dispatch")]
    public IActionResult DispatchTodo(string id, string todoId) =>
        Wrap(_facade.DispatchTodo(id, todoId));

    [HttpPost("sessions/{id}/workspace/snippets")]
    public IActionResult SaveSnippet(string id, [FromBody] WorkspaceWritebackRequest request) =>
        Wrap(_facade.SaveSnippet(id, request));

    [HttpGet("catalog/models")]
    public ActionResult<IReadOnlyList<CatalogModelDto>> Models() => Ok(_facade.ListModels());

    [HttpGet("catalog/agents")]
    public ActionResult<IReadOnlyList<CatalogAgentDto>> Agents() => Ok(_facade.ListAgents());

    private IActionResult Wrap(object? result)
    {
        if (result == null) return NotFound(new { error = "会话不存在" });
        if (result is { } box)
        {
            var error = box.GetType().GetProperty("error")?.GetValue(box) as string;
            if (!string.IsNullOrWhiteSpace(error))
                return BadRequest(new { error });
        }
        return Ok(result);
    }

    private async Task WriteSse(AgentEventDto ev, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(ev, SseJson);
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
