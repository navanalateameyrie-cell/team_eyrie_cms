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
[Route("api/v{version:apiVersion}/clients/{clientId:guid}/service-files/{fileId:guid}/staff-assignments")]
public class ServiceFileStaffAssignmentsController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly StaffAssignmentService _svc;

    public ServiceFileStaffAssignmentsController(CmsDbContext db)
    {
        _db = db;
        _svc = new StaffAssignmentService(db);
    }

    public record FileAssignRequest(
        [Required] Guid StaffId,
        [Required] AssignmentRole Role,
        [Required] DateOnly EffectiveFrom,
        [StringLength(2000)] string? Notes);

    private async Task<bool> FileExistsAsync(Guid clientId, Guid fileId, CancellationToken ct) =>
        await _db.ServiceFiles.AnyAsync(f => f.ClientId == clientId && f.Id == fileId, ct);

    [HttpGet]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<IReadOnlyList<StaffAssignmentService.AssignmentDto>>> List(
        Guid clientId, Guid fileId,
        [FromQuery] DateOnly? asOf,
        [FromQuery] bool includeHistory = false,
        [FromQuery] AssignmentRole? role = null,
        CancellationToken ct = default)
    {
        if (!await FileExistsAsync(clientId, fileId, ct)) return NotFound();
        return await _svc.ListAsync(AssignmentScope.ServiceFile, fileId, asOf, includeHistory, role, ct);
    }

    [HttpPost]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<StaffAssignmentService.AssignmentDto>> Assign(
        Guid clientId, Guid fileId, [FromBody] FileAssignRequest req, CancellationToken ct)
    {
        if (!await FileExistsAsync(clientId, fileId, ct)) return NotFound();
        return await _svc.AssignAsync(AssignmentScope.ServiceFile, fileId, req.StaffId, req.Role,
                                      req.EffectiveFrom, req.Notes, ct);
    }

    [HttpPost("{id:guid}/end")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<StaffAssignmentService.AssignmentDto>> End(
        Guid clientId, Guid fileId, Guid id,
        [FromQuery, Required] int version,
        [FromQuery] DateOnly? effectiveTo,
        CancellationToken ct)
    {
        if (!await FileExistsAsync(clientId, fileId, ct)) return NotFound();
        return await _svc.EndAsync(AssignmentScope.ServiceFile, fileId, id, version, effectiveTo, ct);
    }
}
