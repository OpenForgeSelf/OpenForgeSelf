using ForgeSelf.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Controllers;

/// <summary>
/// 权限策略 API（B4）：profile CRUD + 审计联合查询。
/// 设置页「权限策略」面板与审计视图的数据源（UI 属协同批 2026-11-02-b4-permission-ui）。
/// </summary>
[ApiController]
[Route("api/permissions")]
public sealed class PermissionsController : ControllerBase
{
    private readonly IPermissionProfileService? _profiles;

    /// <summary>宿主未注册策略服务时端点降级 503（不阻断宿主启动）。</summary>
    public PermissionsController(IPermissionProfileService? profiles = null)
    {
        _profiles = profiles;
    }

    private ActionResult? MissingService()
        => _profiles == null ? StatusCode(503, new { error = "宿主未提供权限策略服务" }) : null;

    /// <summary>GET /api/permissions/profiles —— 列出全部 profile（含禁用）。</summary>
    [HttpGet("profiles")]
    public async Task<ActionResult<IReadOnlyList<PermissionProfileDto>>> ListProfiles(CancellationToken ct)
    {
        if (MissingService() is { } m) return m;
        return Ok(await _profiles!.ListAsync(ct).ConfigureAwait(false));
    }

    /// <summary>PUT /api/permissions/profiles/{id} —— 保存（id=0 新建；AgentId 冲突 409）。</summary>
    [HttpPut("profiles/{id:long}")]
    public async Task<ActionResult<PermissionProfileDto>> SaveProfile(long id, [FromBody] PermissionProfileDto profile, CancellationToken ct)
    {
        if (MissingService() is { } m) return m;
        if (profile == null) return BadRequest(new { error = "body 必填" });

        profile.Id = id;
        var saved = await _profiles!.SaveAsync(profile, ct).ConfigureAwait(false);
        if (saved == null)
            return Conflict(new { error = $"AgentId「{profile.AgentId}」已存在策略配置" });
        return Ok(saved);
    }

    /// <summary>DELETE /api/permissions/profiles/{id} —— 删除。</summary>
    [HttpDelete("profiles/{id:long}")]
    public async Task<ActionResult> DeleteProfile(long id, CancellationToken ct)
    {
        if (MissingService() is { } m) return m;
        var deleted = await _profiles!.DeleteAsync(id, ct).ConfigureAwait(false);
        return deleted ? Ok(new { deleted = true }) : NotFound(new { error = $"profile {id} 不存在" });
    }

    /// <summary>
    /// GET /api/permissions/audit —— 审计联合查询（策略决策日志 + ToolApproval），Time 倒序分页。
    /// 回答「谁在何时批了什么」：kind=tool-approval 看人审记录；kind=policy-decision 看策略判定。
    /// </summary>
    [HttpGet("audit")]
    public async Task<ActionResult> QueryAudit(
        [FromQuery] string? toolName,
        [FromQuery] string? sessionId,
        [FromQuery] string? kind,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (MissingService() is { } m) return m;
        if (page < 1) return BadRequest(new { error = "page 从 1 起" });
        if (pageSize is < 1 or > 200) return BadRequest(new { error = "pageSize 取值 1..200" });
        if (kind is not (null or "policy-decision" or "tool-approval"))
            return BadRequest(new { error = "kind 只接受 policy-decision | tool-approval" });

        var (items, total) = await _profiles!.QueryAuditAsync(new PermissionAuditQuery
        {
            SessionId = sessionId,
            ToolName = toolName,
            Kind = kind,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize,
        }, ct).ConfigureAwait(false);
        return Ok(new { items, total, page, pageSize });
    }
}
