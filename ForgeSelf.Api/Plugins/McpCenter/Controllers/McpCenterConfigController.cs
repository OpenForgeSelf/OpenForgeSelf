using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Controllers;

/// <summary>
/// 网关配置查看与修改（v2.0.0 新增）：解决「MCP 网关地址/令牌在哪看、怎么设」。
/// GET 返回脱敏视图（令牌不回显明文）；PUT 校验 → 写 config.json → 热重启内置服务器（失败回滚）。
/// 经插件子 provider 解析（PluginAwareControllerActivator），受宿主统一鉴权保护（管理面鉴权验收标准）。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/mcp-center/config")]
public class McpCenterConfigController : ControllerBase
{
    private readonly McpCenterRuntime _runtime;

    public McpCenterConfigController(McpCenterRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <summary>查看网关配置（监听地址/端口/令牌状态/运行状态）。</summary>
    [HttpGet]
    public ActionResult<ApiResponse<McpCenterConfigDto>> Get()
    {
        return Ok(ApiResponse<McpCenterConfigDto>.Ok(_runtime.GetInfo(), "获取网关配置成功"));
    }

    /// <summary>修改网关配置并热重启生效；失败回滚旧配置并返回错误。</summary>
    [HttpPut]
    public async Task<ActionResult<ApiResponse<McpCenterConfigDto>>> Put([FromBody] McpCenterConfigUpdateDto update)
    {
        try
        {
            var info = await _runtime.ApplyUpdateAsync(update);
            return Ok(ApiResponse<McpCenterConfigDto>.Ok(info, "网关配置已更新并重启生效"));
        }
        catch (ArgumentException ex)
        {
            return StatusCode(400, ApiResponse<McpCenterConfigDto>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新网关配置失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<McpCenterConfigDto>.Error("更新网关配置失败: " + ex.Message));
        }
    }
}
