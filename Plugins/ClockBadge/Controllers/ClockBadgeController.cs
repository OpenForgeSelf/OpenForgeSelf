using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Plugins.ClockBadge.Controllers;

/// <summary>
/// 时钟徽标插件演示端点：GET /api/clock-badge/now → { time, timestamp }。
/// 无管理面写操作（仅只读演示端点），无需 [Authorize("ApiKeyPolicy")]（铁律 17 针对管理/CRUD/配置类控制器）。
/// </summary>
[ApiController]
[Route("api/clock-badge")]
public class ClockBadgeController : ControllerBase
{
    /// <summary>服务器当前时间（本地时区）+ Unix 毫秒时间戳。</summary>
    [HttpGet("now")]
    public IActionResult Now()
    {
        var now = DateTime.Now;
        return Ok(new ClockNowDto
        {
            Time = now.ToString("yyyy-MM-dd HH:mm:ss"),
            Timestamp = new DateTimeOffset(now).ToUnixTimeMilliseconds()
        });
    }
}

/// <summary>GET /api/clock-badge/now 的响应体。time=本地格式化时间，timestamp=Unix 毫秒。</summary>
public class ClockNowDto
{
    public string Time { get; set; } = string.Empty;

    public long Timestamp { get; set; }
}
