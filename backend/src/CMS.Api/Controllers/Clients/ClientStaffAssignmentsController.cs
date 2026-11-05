using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Api.Controllers.Assignments;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/clients/{clientId:guid}/staff-assignments")]
public class ClientStaffAssignmentsController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly StaffAssignmentService _svc;

    public ClientStaffAssignmentsController(CmsDbContext db)
    {
        _db = db;
        _svc = new StaffAssignmentService(db);
    }

    public record AssignRequest(
        [Required] Guid StaffId,
        [Required] AssignmentRole Role,
        [Required] DateOnly EffectiveFrom,
        [StringLength(2000)] string? Notes);

    [HttpGet]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<IReadOnlyList<StaffAssignmentService.AssignmentDto>>> List(
        Guid clientId,
        [FromQuery] DateOnly? asOf,
        [FromQuery] bool includeHistory = false,
        [FromQuery] AssignmentRole? role = null,
        CancellationToken ct = default)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct)) return NotFound();
        return await _svc.ListAsync(AssignmentScope.Client, clientId, asOf, includeHistory, role, ct);
    }

    [HttpPost]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<StaffAssignmentService.AssignmentDto>> Assign(
        Guid clientId, [FromBody] AssignRequest req, CancellationToken ct)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct)) return NotFound();
        return await _svc.AssignAsync(AssignmentScope.Client, clientId, req.StaffId, req.Role,
                                      req.EffectiveFrom, req.Notes, ct);
    }

    [HttpPost("{id:guid}/end")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<StaffAssignmentService.AssignmentDto>> End(
        Guid clientId, Guid id,
        [FromQuery, Required] int version,
        [FromQuery] DateOnly? effectiveTo,
        CancellationToken ct)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct)) return NotFound();
        return await _svc.EndAsync(AssignmentScope.Client, clientId, id, version, effectiveTo, ct);
    }
}
