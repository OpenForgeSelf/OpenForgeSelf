using Microsoft.AspNetCore.Mvc;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Services;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Controllers;

/// <summary>
/// 供应商模型记录管理 API。仅返回模型元数据，绝不返回任何凭证（ApiKey）。
/// 分组列表按供应商聚合，启停与编辑针对单条模型记录。
/// </summary>
[ApiController]
[Route("api/ai-models")]
public class AIModelController : ControllerBase
{
    private readonly IAIModelService _service;
    private readonly ILogService _log;

    public AIModelController(IAIModelService service, ILogService log)
    {
        _service = service;
        _log = log;
    }

    /// <summary>按供应商分组列出模型（providerId 指定供应商；enabledOnly=true 仅已启用）</summary>
    [HttpGet]
    public IActionResult List([FromQuery] long? providerId, [FromQuery] bool enabledOnly = false)
    {
        try
        {
            var groups = _service.GetGroups(providerId, enabledOnly);
            return Ok(new { success = true, data = new { groups } });
        }
        catch (Exception ex)
        {
            _log.Error("获取模型列表失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>启用/禁用指定模型（状态：已启用/已禁用）</summary>
    [HttpPatch("{id}/enabled")]
    public IActionResult SetEnabled(long id, [FromBody] SetEnabledRequest request)
    {
        try
        {
            var dto = _service.SetEnabled(id, request.Enabled);
            if (dto == null) return NotFound(new { success = false, message = "模型不存在" });
            return Ok(new { success = true, data = dto });
        }
        catch (Exception ex)
        {
            _log.Error("切换模型启用状态失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>更新用户可编辑字段（仅 alias/capabilities/maxContext）</summary>
    [HttpPut("{id}")]
    public IActionResult UpdateEditable(long id, [FromBody] UpdateAIModelRequest request)
    {
        try
        {
            var dto = _service.UpdateEditable(id, request.Alias, request.Capabilities, request.MaxContext);
            if (dto == null) return NotFound(new { success = false, message = "模型不存在" });
            return Ok(new { success = true, data = dto });
        }
        catch (Exception ex)
        {
            _log.Error("更新模型失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}

public class SetEnabledRequest
{
    public bool Enabled { get; set; }
}

public class UpdateAIModelRequest
{
    public string? Alias { get; set; }
    public List<string>? Capabilities { get; set; }
    public int? MaxContext { get; set; }
}
