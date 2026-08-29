using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Check()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.Now });
    }
}
