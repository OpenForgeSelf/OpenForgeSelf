using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using XCode;

namespace ForgeSelf.Api.Plugins.AgentHub.Controllers;

/// <summary>
/// Session 管理面（P3）：列表 / 详情 / 输入 / 关闭。对外只暴露 SessionKey。
/// 路由前缀 <c>api/agent-hub/sessions</c>。
/// </summary>
[ApiController]
[Route("api/agent-hub/sessions")]
public class AgentHubSessionsController : ControllerBase
{
    private readonly AgentSessionManager _sessions;
    private readonly IAgentRuntime _runtime;

    public AgentHubSessionsController(AgentSessionManager sessions, IAgentRuntime runtime)
    {
        _sessions = sessions;
        _runtime = runtime;
    }

    /// <summary>列出最近会话（默认最多 50；不含 OarSessionId）。</summary>
    [HttpGet]
    public ActionResult<ApiResponse<IReadOnlyList<SessionDto>>> List([FromQuery] Int32 limit = 50)
    {
        var take = Math.Clamp(limit, 1, 200);
        var entities = Entities.AgentHubSession.FindAll(
            null,
            Entities.AgentHubSession._.UpdateTime.Desc(),
            null, 0, take);
        var list = entities.Select(ToDto).ToList();
        return Ok(ApiResponse<IReadOnlyList<SessionDto>>.Ok(list, $"共 {list.Count} 条"));
    }

    /// <summary>按 SessionKey 取详情。</summary>
    [HttpGet("{sessionKey}")]
    public ActionResult<ApiResponse<SessionDto>> Get(String sessionKey)
    {
        var entity = _sessions.Find(sessionKey);
        return entity == null
            ? NotFound(ApiResponse<SessionDto>.Error($"会话 {sessionKey} 不存在", 404))
            : Ok(ApiResponse<SessionDto>.Ok(ToDto(entity)));
    }

    /// <summary>向会话投递输入（经 IAgentRuntime.SendInputAsync）。</summary>
    [HttpPost("{sessionKey}/input")]
    public async Task<ActionResult<ApiResponse<AgentControlResult>>> Input(
        String sessionKey,
        [FromBody] SessionInputRequest body,
        CancellationToken ct)
    {
        if (body == null || body.Text.IsNullOrWhiteSpace())
            return BadRequest(ApiResponse<AgentControlResult>.Error("text 不能为空", 400));

        var result = await _runtime.SendInputAsync(sessionKey, new AgentInput
        {
            Text = body.Text!,
            Mode = body.Mode,
        }, ct).ConfigureAwait(false);

        return result.Accepted
            ? Ok(ApiResponse<AgentControlResult>.Ok(result))
            : BadRequest(ApiResponse<AgentControlResult>.Error(result.Reason ?? result.Code ?? "rejected", 400));
    }

    /// <summary>关闭会话。</summary>
    [HttpPost("{sessionKey}/close")]
    public async Task<ActionResult<ApiResponse>> Close(String sessionKey, CancellationToken ct)
    {
        await _runtime.CloseSessionAsync(sessionKey, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Ok($"会话 {sessionKey} 已关闭"));
    }

    private static SessionDto ToDto(Entities.AgentHubSession e) => new()
    {
        SessionKey = e.SessionKey,
        AgentId = e.AgentId,
        AccessPointId = e.AccessPointId,
        Transport = e.Transport,
        RuntimeId = e.RuntimeId,
        Cwd = e.Cwd,
        Model = e.Model,
        Status = e.Status,
        Generation = e.Generation,
        LastTaskId = e.LastTaskId,
        CreateTime = e.CreateTime,
        UpdateTime = e.UpdateTime,
    };
}

/// <summary>会话对外 DTO（无 OarSessionId）。</summary>
public class SessionDto
{
    public String SessionKey { get; set; } = String.Empty;
    public Int32 AgentId { get; set; }
    public Int32 AccessPointId { get; set; }
    public String? Transport { get; set; }
    public String? RuntimeId { get; set; }
    public String? Cwd { get; set; }
    public String? Model { get; set; }
    public String? Status { get; set; }
    public Int32 Generation { get; set; }
    public Int32 LastTaskId { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>会话输入请求体。</summary>
public class SessionInputRequest
{
    public String? Text { get; set; }
    public String? Mode { get; set; }
}
