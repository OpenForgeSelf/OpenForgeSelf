using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Models.Mcp;
using OpenForgeSelf.Backend.Services.Mcp;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Controllers;

[ApiController]
[Route("api/mcp")]
public class McpController : ControllerBase
{
    private readonly IMcpService _mcpService;

    public McpController(IMcpService mcpService)
    {
        _mcpService = mcpService;
    }

    /// <summary>
    /// 获取 MCP 服务器列表
    /// </summary>
    [HttpGet("servers")]
    public async Task<ActionResult<ApiResponse<List<McpServerDto>>>> GetServers()
    {
        try
        {
            var servers = await _mcpService.GetServersAsync();
            return Ok(ApiResponse<List<McpServerDto>>.Ok(servers, "获取 MCP 服务器列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取 MCP 服务器列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<McpServerDto>>.Error("获取 MCP 服务器列表失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取某服务器的工具列表
    /// </summary>
    [HttpGet("servers/{serverId}/tools")]
    public async Task<ActionResult<ApiResponse<List<McpToolDto>>>> GetTools(
        string serverId,
        [FromQuery] string? keyword = null,
        [FromQuery] string? category = null)
    {
        try
        {
            var tools = await _mcpService.GetToolsAsync(serverId, keyword, category);
            return Ok(ApiResponse<List<McpToolDto>>.Ok(tools, "获取工具列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取 MCP 工具列表失败 [serverId={0}]: {1}", serverId, ex.Message);
            return StatusCode(500, ApiResponse<List<McpToolDto>>.Error("获取工具列表失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 切换工具启用/禁用状态
    /// </summary>
    [HttpPost("tools/{toolId}/toggle")]
    public async Task<ActionResult<ApiResponse<McpToolDto>>> ToggleTool(string toolId)
    {
        try
        {
            var tool = await _mcpService.ToggleToolAsync(toolId);
            if (tool == null)
            {
                return NotFound(ApiResponse<McpToolDto>.Error("工具不存在", 404));
            }

            var statusText = tool.IsEnabled ? "启用" : "禁用";
            return Ok(ApiResponse<McpToolDto>.Ok(tool, $"工具 '{tool.Name}' 已{statusText}"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("切换工具状态失败 [toolId={0}]: {1}", toolId, ex.Message);
            return StatusCode(500, ApiResponse<McpToolDto>.Error("切换工具状态失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 测试工具
    /// </summary>
    [HttpPost("tools/{toolId}/test")]
    public async Task<ActionResult<ApiResponse<McpTestResultDto>>> TestTool(string toolId)
    {
        try
        {
            var result = await _mcpService.TestToolAsync(toolId);
            return Ok(ApiResponse<McpTestResultDto>.Ok(result, result.Success ? "测试通过" : "测试失败"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("测试工具失败 [toolId={0}]: {1}", toolId, ex.Message);
            return StatusCode(500, ApiResponse<McpTestResultDto>.Error("测试工具失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 测试服务器连接
    /// </summary>
    [HttpGet("servers/{serverId}/test")]
    public async Task<ActionResult<ApiResponse<McpTestResultDto>>> TestServer(string serverId)
    {
        try
        {
            var result = await _mcpService.TestServerAsync(serverId);
            return Ok(ApiResponse<McpTestResultDto>.Ok(result, result.Success ? "连接正常" : "连接失败"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("测试服务器连接失败 [serverId={0}]: {1}", serverId, ex.Message);
            return StatusCode(500, ApiResponse<McpTestResultDto>.Error("测试服务器连接失败: " + ex.Message));
        }
    }
}