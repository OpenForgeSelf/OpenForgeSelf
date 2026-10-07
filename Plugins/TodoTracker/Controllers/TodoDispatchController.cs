using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.TodoTracker.Controllers;

/// <summary>
/// 下发与委派接口（PILOT-054 · FR-1.2 / FR-5 / FR-6）。
///
/// 「一键交给 AgentHub 执行」经能力接缝 <see cref="IAgentDelegation"/>（提供方 = agent-hub，
/// 见 <c>Plugins/AgentHub/AgentHubPlugin.cs</c> 的 <c>ctx.Register</c>），
/// <b>不是</b>本插件去直连 agent-hub 的 HTTP 端口（architecture-design 铁律 2/3）。
/// 接缝缺席 ⇒ <c>503 + 原文</c>，绝不静默当成功。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/todos")]
public class TodoDispatchController : ControllerBase
{
    private readonly ITodoService _todos;
    private readonly ITodoDispatchService _dispatch;

    public TodoDispatchController(ITodoService todos, ITodoDispatchService dispatch)
    {
        _todos = todos;
        _dispatch = dispatch;
    }

    /// <summary>下发（校验必填 → 已下发 + 留痕）。Draft 起步会自动补一跳「就绪」。</summary>
    [HttpPost("{id:int}/dispatch")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> Dispatch(int id, [FromQuery] string? assignee = null)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse<TodoDto>.Error("id 必须为正整数", 400));

            var result = await _dispatch.DispatchAsync(id, assignee, ActorHint());
            return Map(result, "下发成功");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<TodoDto>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("下发待办失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("下发失败: " + ex.Message));
        }
    }

    /// <summary>一键交给 AgentHub 执行（入队即返回；进展看 agent-status）。</summary>
    [HttpPost("{id:int}/dispatch-to-agent")]
    public async Task<ActionResult<ApiResponse<DelegateToAgentResultDto>>> DelegateToAgent(int id,
        [FromBody] DelegateToAgentRequest? request)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse<DelegateToAgentResultDto>.Error("id 必须为正整数", 400));

            var result = await _dispatch.DelegateAsync(id, request, ActorHint(), $"{Request.Scheme}://{Request.Host}");
            if (result.Ok)
            {
                var note = result.BackfillWarning ?? $"任务 {result.TaskKey} 已交给 {result.AgentName}（{result.Status}）";
                return Ok(ApiResponse<DelegateToAgentResultDto>.Ok(result, note));
            }

            var reason = result.Error ?? "委派失败";
            // 接缝缺席与 AgentHub 的业务裁决要分得开：前者 503（用户该去装/启用 agent-hub），
            // 后者 400（用户该改 cwd 白名单 / 换 agent / 换权限模式），原因原文一律透传。
            // 判据是服务给的 SeamMissing 标志，不是原因文本 —— 措辞改了映射就不能坏了。
            return result.SeamMissing
                ? StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<DelegateToAgentResultDto>.Error(reason, 503))
                : BadRequest(ApiResponse<DelegateToAgentResultDto>.Error(reason, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("一键委派失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<DelegateToAgentResultDto>.Error("一键委派失败: " + ex.Message));
        }
    }

    /// <summary>回读委派任务状态（Queued/Running/…/Succeeded/Failed/…）。</summary>
    [HttpGet("{id:int}/agent-status")]
    public async Task<ActionResult<ApiResponse<AgentStatusDto>>> AgentStatus(int id)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse<AgentStatusDto>.Error("id 必须为正整数", 400));

            var status = await _dispatch.AgentStatusAsync(id);
            return StatusResult(status);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("读回委派状态失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<AgentStatusDto>.Error("读回委派状态失败: " + ex.Message));
        }
    }

    /// <summary>把 agent 的结果落成一条执行记录（成功⇒待验收；未成功只记账不动状态）。</summary>
    [HttpPost("{id:int}/agent-status/record")]
    public async Task<ActionResult<ApiResponse<RecordAgentResultDto>>> RecordAgentResult(int id)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse<RecordAgentResultDto>.Error("id 必须为正整数", 400));

            var result = await _dispatch.RecordAgentResultAsync(id, ActorHint());
            return result.Ok
                ? Ok(ApiResponse<RecordAgentResultDto>.Ok(result, "agent 结果已记入执行记录"))
                : BadRequest(ApiResponse<RecordAgentResultDto>.Error(result.Error ?? "记入失败", 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("回写 agent 结果失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<RecordAgentResultDto>.Error("回写 agent 结果失败: " + ex.Message));
        }
    }

    /// <summary>
    /// agent 领取下一条已下发任务（领到即置执行中并留痕）。
    /// 没有可领的任务 ⇒ <c>204 No Content</c>（空态不是错误，agent 据此继续等待而不是报错退出）。
    /// </summary>
    [HttpGet("agent/next")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> ClaimNext([FromQuery] string? assignee = null,
        [FromQuery] int projectId = 0)
    {
        try
        {
            var result = await _dispatch.ClaimNextAsync(assignee, projectId, ActorHint());
            if (!result.Ok)
                return BadRequest(ApiResponse<TodoDto>.Error(result.Error ?? "领取失败", 400));

            return result.Data == null
                ? NoContent()
                : Ok(ApiResponse<TodoDto>.Ok(result.Data, $"已领取任务 {result.Data.TaskKey}"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("领取任务失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("领取任务失败: " + ex.Message));
        }
    }

    private ActionResult<ApiResponse<TodoDto>> Map(TodoOpResult result, string okMessage)
    {
        if (result.Ok && result.Data != null) return Ok(ApiResponse<TodoDto>.Ok(result.Data, okMessage));

        var reason = result.Error ?? "操作失败";
        return result.StatusCode switch
        {
            404 => NotFound(ApiResponse<TodoDto>.Error(reason, 404)),
            409 => Conflict(ApiResponse<TodoDto>.Error(reason, 409)),
            503 => StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<TodoDto>.Error(reason, 503)),
            _ => BadRequest(ApiResponse<TodoDto>.Error(reason, 400))
        };
    }

    private ActionResult<ApiResponse<AgentStatusDto>> StatusResult(AgentStatusDto status)
    {
        if (status.Ok) return Ok(ApiResponse<AgentStatusDto>.Ok(status, "获取委派状态成功"));

        return status.StatusCode switch
        {
            404 => NotFound(ApiResponse<AgentStatusDto>.Error(status.Error ?? "委派任务不存在", 404)),
            503 => StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<AgentStatusDto>.Error(status.Error ?? "委派能力缺席", 503)),
            _ => BadRequest(ApiResponse<AgentStatusDto>.Error(status.Error ?? "读取委派状态失败", 400))
        };
    }

    private string ActorHint()
    {
        var header = Request.Headers["X-Actor"].ToString();
        return string.IsNullOrWhiteSpace(header) ? "rest" : header.Trim();
    }
}
