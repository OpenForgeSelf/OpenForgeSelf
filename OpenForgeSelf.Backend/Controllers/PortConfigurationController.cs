using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenForgeSelf.Backend.Services;

namespace OpenForgeSelf.Backend.Controllers;

/// <summary>端口配置控制器</summary>
[ApiController]
[Route("api/[controller]")]
public class PortConfigurationController : ControllerBase
{
  private readonly IPortConfigurationService _service;
  private readonly ILogger<PortConfigurationController> _logger;

  public PortConfigurationController(
      IPortConfigurationService service,
      ILogger<PortConfigurationController> logger)
  {
    _service = service;
    _logger = logger;
  }

  /// <summary>获取当前配置的端口</summary>
  /// <returns>端口配置信息</returns>
  [HttpGet]
  public async Task<ActionResult<object>> GetPort()
  {
    var port = await _service.GetPortAsync();

    if (port == null || port.Value <= 0)
      return NotFound(new { success = false, message = "未找到端口配置" });

    return Ok(new
    {
      success = true,
      message = "端口配置获取成功",
      data = new PortConfigDto
      {
        PortNumber = port.Value,
        Message = "端口配置获取成功"
      }
    });
  }

  /// <summary>设置监听端口</summary>
  /// <param name="request">端口配置请求</param>
  /// <returns>更新结果</returns>
  [HttpPost]
  [Authorize("ApiKeyPolicy")]
  public async Task<ActionResult<object>> SetPort([FromBody] SetPortRequest request)
  {
    if (request == null || request.PortNumber < 1024 || request.PortNumber > 65535)
      return BadRequest(new { success = false, message = "端口号必须在 1024-65535 范围内" });

    try
    {
      var port = await _service.SetPortAsync(request.PortNumber);

      return Ok(new
      {
        success = true,
        message = "端口配置更新成功",
        data = new PortConfigDto
        {
          PortNumber = port,
          Message = "端口配置更新成功"
        }
      });
    }
    catch (ArgumentOutOfRangeException ex)
    {
      return BadRequest(new { success = false, message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
      return Conflict(new { success = false, message = ex.Message });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "设置端口失败：{Port}", request.PortNumber);
      return StatusCode(500, new { success = false, message = "设置端口失败", error = ex.Message });
    }
  }

  /// <summary>检查端口是否可用</summary>
  /// <param name="port">端口号</param>
  /// <returns>可用性信息</returns>
  [HttpGet("check/{port}")]
  public async Task<ActionResult<object>> CheckPort(Int32 port)
  {
    if (port < 1024 || port > 65535)
      return BadRequest(new { success = false, message = "端口号必须在 1024-65535 范围内" });

    var available = await _service.IsPortAvailableAsync(port);

    return Ok(new
    {
      success = true,
      message = available ? "端口可用" : "端口已被占用",
      data = new PortAvailabilityDto
      {
        PortNumber = port,
        IsAvailable = available,
        Message = available ? "端口可用" : "端口已被占用"
      }
    });
  }
}

/// <summary>端口配置响应 DTO</summary>
public class PortConfigDto
{
  /// <summary>端口号</summary>
  public Int32 PortNumber { get; set; }

  /// <summary>消息</summary>
  public String Message { get; set; } = String.Empty;
}

/// <summary>设置端口请求 DTO</summary>
public class SetPortRequest
{
  /// <summary>端口号（1024-65535）</summary>
  public Int32 PortNumber { get; set; }
}

/// <summary>端口可用性响应 DTO</summary>
public class PortAvailabilityDto
{
  /// <summary>端口号</summary>
  public Int32 PortNumber { get; set; }

  /// <summary>是否可用</summary>
  public Boolean IsAvailable { get; set; }

  /// <summary>消息</summary>
  public String Message { get; set; } = String.Empty;
}
