using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Controllers;

/// <summary>
/// dsh（DeepSeek Harness）MCP 配置管理（v2.3.0）：可视化管理 dsh 补丁层 cordis.patch.yml 里的
/// 全部 MCP 客户端条目（name = @deepseek-ai/dsh-mcp-client）——列出当前配置 / 新增 / 编辑 / 启停 / 删除 / 去重。
/// 管理面鉴权（铁律 17）：类级 [Authorize("ApiKeyPolicy")]，未带宿主令牌一律 401。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/mcp-center/dsh")]
public class McpCenterDshController : ControllerBase
{
    private readonly DshMcpConfigService _service;

    public McpCenterDshController(DshMcpConfigService service)
    {
        _service = service;
    }

    /// <summary>列出当前 dsh MCP 配置（文件路径、是否存在、全部条目、重复 id）。</summary>
    [HttpGet]
    public ActionResult<ApiResponse<DshMcpConfigDto>> Get([FromQuery] string? profile)
    {
        return Ok(ApiResponse<DshMcpConfigDto>.Ok(_service.GetStatus(profile), "获取 dsh MCP 配置成功"));
    }

    /// <summary>新增一条 MCP 客户端条目。</summary>
    [HttpPost("servers")]
    public ActionResult<ApiResponse<DshMcpConfigDto>> Add([FromQuery] string? profile, [FromBody] DshMcpServerUpsertDto dto)
    {
        return Run(() => _service.Add(profile, dto), "已新增 dsh MCP 条目");
    }

    /// <summary>编辑一条 MCP 客户端条目（id 不可改）。</summary>
    [HttpPut("servers/{id}")]
    public ActionResult<ApiResponse<DshMcpConfigDto>> Update(string id, [FromQuery] string? profile, [FromBody] DshMcpServerUpsertDto dto)
    {
        return Run(() => _service.Update(profile, id, dto), "已更新 dsh MCP 条目");
    }

    /// <summary>启用/停用一条 MCP 客户端条目（写补丁层 disabled 行）。</summary>
    [HttpPost("servers/{id}/toggle")]
    public ActionResult<ApiResponse<DshMcpConfigDto>> Toggle(string id, [FromQuery] string? profile, [FromQuery] bool enabled = true)
    {
        return Run(() => _service.Toggle(profile, id, enabled), enabled ? "已启用" : "已停用");
    }

    /// <summary>删除一条 MCP 客户端条目（含其重复项）。</summary>
    [HttpDelete("servers/{id}")]
    public ActionResult<ApiResponse<DshMcpConfigDto>> Delete(string id, [FromQuery] string? profile)
    {
        return Run(() => _service.Delete(profile, id), "已删除 dsh MCP 条目");
    }

    /// <summary>一键去重：同一 id 只保留第一条。</summary>
    [HttpPost("dedupe")]
    public ActionResult<ApiResponse<DshMcpConfigDto>> Dedupe([FromQuery] string? profile)
    {
        return Run(() => _service.Dedupe(profile), "已去重 dsh MCP 条目");
    }

    /// <summary>快捷写入 ForgeSelf 条目地址（旧版兼容）。</summary>
    [HttpPost]
    public ActionResult<ApiResponse<DshMcpConfigDto>> QuickWrite([FromBody] DshMcpConfigWriteDto dto)
    {
        var d = dto ?? new DshMcpConfigWriteDto();
        return Run(() => _service.QuickWrite(d.Profile, d.ServerId, d.ServerName, d.Url), "已写入 dsh MCP 配置");
    }

    private ActionResult<ApiResponse<DshMcpConfigDto>> Run(Func<DshMcpConfigDto> action, string okMessage)
    {
        try
        {
            return Ok(ApiResponse<DshMcpConfigDto>.Ok(action(), okMessage));
        }
        catch (ArgumentException ex)
        {
            return StatusCode(400, ApiResponse<DshMcpConfigDto>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("dsh MCP 配置操作失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<DshMcpConfigDto>.Error("dsh MCP 配置操作失败: " + ex.Message));
        }
    }
}
