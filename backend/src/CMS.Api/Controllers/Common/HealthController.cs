using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>Returns a static liveness marker. Dependency probes live at /health/ready and /health/live.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = "CMS.Api",
        version = "1.0",
        utc = DateTimeOffset.UtcNow
    });
}
