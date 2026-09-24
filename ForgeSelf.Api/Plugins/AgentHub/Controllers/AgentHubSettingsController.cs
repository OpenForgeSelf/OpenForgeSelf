using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Controllers;

/// <summary>
/// Agent 中枢插件设置接口：附加扫描目录等（探测行为配置）。
/// 路由前缀 <c>api/agent-hub/settings</c>。
/// 铁律 17：插件管理/CRUD/配置控制器必须类级 <c>[Authorize("ApiKeyPolicy")]</c>。
/// </summary>
[ApiController]
[Route("api/agent-hub/settings")]
[Authorize("ApiKeyPolicy")]
public class AgentHubSettingsController : ControllerBase
{
    private readonly AgentHubSettingsStore _store;

    public AgentHubSettingsController(AgentHubSettingsStore store)
    {
        _store = store;
    }

    /// <summary>读取当前设置。</summary>
    /// <returns>设置（含附加扫描目录列表）</returns>
    [HttpGet]
    public ActionResult<ApiResponse<AgentHubSettingsDto>> Get()
    {
        var s = _store.Current;
        return Ok(ApiResponse<AgentHubSettingsDto>.Ok(new AgentHubSettingsDto
        {
            SearchDirectories = s.SearchDirectories
        }));
    }

    /// <summary>保存设置（规范化：去空白/去重/去空项，点即保存落盘）。</summary>
    /// <param name="request">请求体</param>
    /// <returns>保存后的设置</returns>
    [HttpPut]
    public ActionResult<ApiResponse<AgentHubSettingsDto>> Save([FromBody] AgentHubSettingsDto request)
    {
        try
        {
            var saved = _store.Save(new AgentHubSettings
            {
                SearchDirectories = request?.SearchDirectories ?? new List<String>()
            });
            return Ok(ApiResponse<AgentHubSettingsDto>.Ok(new AgentHubSettingsDto
            {
                SearchDirectories = saved.SearchDirectories
            }, $"已保存 {saved.SearchDirectories.Count} 个附加扫描目录，重新扫描即生效"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AgentHub] 保存设置失败: {0}", ex.Message);
            return BadRequest(ApiResponse<AgentHubSettingsDto>.Error(ex.Message, 400));
        }
    }
}
