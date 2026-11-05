using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Infrastructure.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Api.Controllers;

/// <summary>Admin-triggered manual run of the Monday weekly summary.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/summary")]
public class SummaryController : ControllerBase
{
    private readonly IWeeklySummaryService _svc;
    public SummaryController(IWeeklySummaryService svc) => _svc = svc;

    [HttpPost("weekly/run")]
    [HasPermission(Permissions.Reports.Read)]
    public async Task<ActionResult<WeeklySummaryRunResult>> Run(
        [FromQuery] bool dryRun = false, [FromQuery] DateOnly? weekStart = null, CancellationToken ct = default)
    {
        var r = await _svc.RunAsync(dryRun, weekStart, ct);
        return r;
    }
}
