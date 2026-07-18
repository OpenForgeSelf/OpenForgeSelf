using Microsoft.AspNetCore.Mvc;

namespace OpenForgeSelf.Backend.Controllers;

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
