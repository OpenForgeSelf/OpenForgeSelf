using ForgeSelf.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.BrowserUse;

/// <summary>浏览器单实例只读状态。不打开、不操作页面。</summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/browser-use")]
public sealed class BrowserUseController : ControllerBase
{
    private readonly BrowserSession _session;

    public BrowserUseController(BrowserSession session)
    {
        _session = session;
    }

    /// <summary>GET /api/browser-use/session</summary>
    [HttpGet("session")]
    public ActionResult<ApiResponse<BrowserSessionView>> Session()
    {
        return Ok(ApiResponse<BrowserSessionView>.Ok(_session.Describe(), "浏览器状态"));
    }
}
