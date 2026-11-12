using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using ForgeSelf.Api.Plugins.SmartTerminal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Controllers;

/// <summary>
/// 智能终端 v2。前缀 /api/smart-terminal；主路径 POST /sessions/{id}/commands。
/// 类级 ApiKeyPolicy（plugin-development 铁律 17）。错误体 {code, message}。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/smart-terminal")]
public class SmartTerminalController : ControllerBase
{
    private readonly ISmartTerminalV2 _v2;
    private readonly ITerminalSessionService _legacy;

    public SmartTerminalController(ISmartTerminalV2 v2, ITerminalSessionService legacy)
    {
        _v2 = v2;
        _legacy = legacy;
    }

    [HttpGet("meta")]
    public IActionResult Meta() => Ok(_v2.GetMeta());

    [HttpGet("shells")]
    public IActionResult ListShells() => Ok(_legacy.ListShells());

    [HttpGet("permission-mode")]
    public IActionResult GetPermissionMode()
        => Ok(new { mode = _legacy.GetGlobalPermissionMode().ToString(), sessionDefault = "AskWrite" });

    [HttpPut("permission-mode")]
    public IActionResult SetPermissionMode([FromBody] SetPermissionModeRequest request)
    {
        _legacy.SetGlobalPermissionMode(request.Mode);
        return Ok(new { mode = request.Mode.ToString() });
    }

    [HttpPut("sessions/{id}/permission-mode")]
    public IActionResult SetSessionPermissionMode(string id, [FromBody] SetPermissionModeRequest request)
    {
        try
        {
            _v2.SetSessionPermissionMode(id, request.Mode);
            return Ok(new { mode = request.Mode.ToString() });
        }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpPost("classify")]
    public IActionResult Classify([FromBody] RunCommandRequest? request)
    {
        try { return Ok(_v2.PreviewClassify(request?.Command ?? string.Empty, null)); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpGet("profiles")]
    public IActionResult ListProfiles([FromQuery] string? kind, [FromQuery] string? q)
        => Ok(_v2.ListProfiles(kind, q));

    [HttpPost("profiles")]
    public IActionResult CreateProfile([FromBody] UpsertProfileRequest? request)
    {
        try { return StatusCode(201, _v2.CreateProfile(request ?? new UpsertProfileRequest(), Source())); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpGet("profiles/{id}")]
    public IActionResult GetProfile(string id)
    {
        try { return Ok(_v2.GetProfile(id)); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpPut("profiles/{id}")]
    public IActionResult UpdateProfile(string id, [FromBody] UpsertProfileRequest? request)
    {
        try { return Ok(_v2.UpdateProfile(id, request ?? new UpsertProfileRequest())); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpDelete("profiles/{id}")]
    public IActionResult DeleteProfile(string id, [FromQuery] bool deleteSecret = false)
    {
        try
        {
            if (!_v2.DeleteProfile(id, deleteSecret)) return Err(SmartTerminalException.NotFound("配置不存在"));
            return NoContent();
        }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpGet("secrets")]
    public IActionResult ListSecrets() => Ok(_v2.ListSecrets());

    [HttpPost("secrets")]
    public IActionResult CreateSecret([FromBody] CreateSecretRequest? request)
    {
        try { return StatusCode(201, _v2.CreateSecret(request ?? new CreateSecretRequest())); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpDelete("secrets/{id}")]
    public IActionResult DeleteSecret(string id)
    {
        try
        {
            if (!_v2.DeleteSecret(id)) return Err(SmartTerminalException.NotFound("凭据不存在"));
            return NoContent();
        }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpGet("snippets")]
    public IActionResult ListSnippets() => Ok(_v2.ListSnippets());

    [HttpPost("snippets")]
    public IActionResult CreateSnippet([FromBody] UpsertSnippetRequest? request)
    {
        try { return StatusCode(201, _v2.CreateSnippet(request ?? new UpsertSnippetRequest())); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpPut("snippets/{id}")]
    public IActionResult UpdateSnippet(string id, [FromBody] UpsertSnippetRequest? request)
    {
        try { return Ok(_v2.UpdateSnippet(id, request ?? new UpsertSnippetRequest())); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpDelete("snippets/{id}")]
    public IActionResult DeleteSnippet(string id)
    {
        try
        {
            if (!_v2.DeleteSnippet(id)) return Err(SmartTerminalException.NotFound("片段不存在"));
            return NoContent();
        }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpGet("sessions")]
    public IActionResult ListSessions() => Ok(_v2.ListOpenSessions());

    [HttpPost("sessions")]
    public IActionResult OpenSession([FromBody] CreateSessionRequest? request)
    {
        if (RejectPasswordInQuery() is { } blocked) return blocked;
        try
        {
            var dto = _v2.OpenSession(request ?? new CreateSessionRequest(), Source(), IsAgent());
            return StatusCode(201, dto);
        }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpGet("sessions/{id}")]
    public IActionResult GetSession(string id)
    {
        try { return Ok(_v2.GetOpenSession(id)); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpPatch("sessions/{id}")]
    public IActionResult PatchSession(string id, [FromBody] PatchSessionRequest? request)
    {
        try { return Ok(_v2.PatchTitle(id, request?.Title)); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpPost("sessions/{id}/disconnect")]
    public IActionResult Disconnect(string id)
    {
        try { return Ok(_v2.Disconnect(id)); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpPost("sessions/{id}/reconnect")]
    public IActionResult Reconnect(string id, [FromBody] ReconnectRequest? request)
    {
        if (RejectPasswordInQuery() is { } blocked) return blocked;
        try { return Ok(_v2.Reconnect(id, request, Source(), IsAgent())); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpPost("sessions/{id}/close")]
    public IActionResult Close(string id)
    {
        try { return Ok(_v2.CloseV2(id)); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpPost("sessions/{id}/commands")]
    public IActionResult SubmitCommand(string id, [FromBody] SubmitCommandRequest? request)
    {
        try
        {
            var result = _v2.SubmitCommand(id, request ?? new SubmitCommandRequest(), Source());
            return StatusCode(202, result);
        }
        catch (Exception ex) { return Err(ex); }
    }

    /// <summary>旧入口别名，语义对齐 POST /commands。</summary>
    [HttpPost("sessions/{id}/run")]
    public IActionResult RunAlias(string id, [FromBody] SubmitCommandRequest? request)
        => SubmitCommand(id, request);

    [HttpGet("sessions/{id}/blocks")]
    public IActionResult ListBlocks(string id, [FromQuery] int afterSeq = 0)
    {
        try { return Ok(_v2.ListBlocks(id, afterSeq)); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpGet("sessions/{id}/commands")]
    public IActionResult CommandHistory(string id)
    {
        try { return Ok(_v2.ListCommandHistory(id)); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpGet("sessions/{id}/chat")]
    public IActionResult GetChat(string id)
    {
        try { return Ok(_v2.GetChat(id)); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpPost("sessions/{id}/chat/messages")]
    public async Task<IActionResult> SendChat(string id, [FromBody] SendChatRequest? request, CancellationToken ct)
    {
        try { return Ok(await _v2.SendChatAsync(id, request ?? new SendChatRequest(), ct)); }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpGet("approvals")]
    public IActionResult ListApprovals([FromQuery] string? sessionId, [FromQuery] string? status = "pending")
        => Ok(_v2.ListApprovalsV2(sessionId, status));

    [HttpPost("approvals/{id}/resolve")]
    public IActionResult Resolve(string id, [FromBody] ResolveApprovalRequest? request)
    {
        try
        {
            var result = _v2.ResolveApprovalV2(id, request?.Decision ?? string.Empty, Source(), IsAgent());
            return Ok(result);
        }
        catch (Exception ex) { return Err(ex); }
    }

    [HttpGet("audit")]
    public IActionResult Audit([FromQuery] string? sessionId, [FromQuery] string? kind, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(_v2.QueryAuditV2(sessionId, kind, q, page, pageSize));

    /// <summary>沿用旧 launch；密码只能在请求体，禁止进 URL。缺密 SSH 改为 409 password_required。</summary>
    [HttpPost("launch")]
    public IActionResult Launch([FromBody] LaunchTerminalRequest? request)
    {
        if (RejectPasswordInQuery() is { } blocked) return blocked;
        try
        {
            var result = _legacy.Launch(request ?? new LaunchTerminalRequest());
            return Ok(result);
        }
        catch (Exception ex) { return Err(ex); }
    }

    /// <summary>拒绝 URL / query 中的口令键（与 SecretRedactor.IsSensitiveQueryKey 对齐）。</summary>
    private IActionResult? RejectPasswordInQuery()
    {
        if (Request.Query.Keys.Any(SecretRedactor.IsSensitiveQueryKey))
            return Err(new SmartTerminalException("invalid_request", "密码不得进入 URL", 400));
        return null;
    }

    private string Source() => StCallerIdentity.Resolve(HttpContext);
    private bool IsAgent() => Source() == StCallerIdentity.Agent;

    private IActionResult Err(Exception ex)
    {
        if (ex is InteractivePasswordRequiredException ip)
        {
            return StatusCode(409, new Dictionary<string, object?>
            {
                ["code"] = "password_required",
                ["message"] = SecretRedactor.Redact(ip.Message),
                ["needsInteractivePassword"] = true
            });
        }

        if (ex is SmartTerminalException st)
        {
            var body = new Dictionary<string, object?>
            {
                ["code"] = st.Code,
                ["message"] = SecretRedactor.Redact(st.Message)
            };
            foreach (var kv in st.Extra)
                body[kv.Key] = kv.Value;
            XTrace.Log.Info("[smart-terminal] {0}: {1}", st.Code, body["message"]);
            return StatusCode(st.StatusCode, body);
        }

        if (ex is KeyNotFoundException)
            return StatusCode(404, new { code = "not_found", message = SecretRedactor.Redact(ex.Message) });

        XTrace.Log.Warn("[smart-terminal] {0}", SecretRedactor.Redact(ex.Message));
        return StatusCode(400, new { code = "invalid_request", message = SecretRedactor.Redact(ex.Message) });
    }
}
