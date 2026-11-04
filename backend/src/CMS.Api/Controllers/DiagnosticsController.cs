using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Api.Controllers;

/// <summary>
/// Permission-protected probe endpoints. Live only to prove 401/403/200 wiring end-to-end;
/// no business behavior. Business modules take over from Phase 3.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class DiagnosticsController : ControllerBase
{
    [HttpGet("authenticated")]
    [Authorize]
    public IActionResult Authenticated() => Ok(new { ok = true, scope = "any-authenticated-user" });

    [HttpGet("clients-read")]
    [HasPermission(Permissions.Clients.Read)]
    public IActionResult ClientsRead() => Ok(new { ok = true, scope = Permissions.Clients.Read });

    [HttpGet("users-manage")]
    [HasPermission(Permissions.Users.Manage)]
    public IActionResult UsersManage() => Ok(new { ok = true, scope = Permissions.Users.Manage });

    /// <summary>Deliberate 500 for exercising the global exception handler + correlation surfacing.</summary>
    [HttpGet("throw")]
    [AllowAnonymous]
    public IActionResult Throw() => throw new InvalidOperationException("deliberate test boom");
}
