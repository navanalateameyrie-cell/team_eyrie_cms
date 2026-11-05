using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
public class WorkloadController : ControllerBase
{
    private readonly CmsDbContext _db;
    public WorkloadController(CmsDbContext db) => _db = db;

    public record StaffWorkloadSummary(
        Guid StaffId, string StaffCode, string StaffFullName, bool StaffIsActive,
        int ClientPrimaryCount, int ClientSecondaryCount, int ClientReviewerCount,
        int FilePrimaryCount, int FileSecondaryCount, int FileReviewerCount,
        int TotalClients, int TotalFiles);

    public record WorkloadRow(
        Guid AssignmentId, AssignmentScope Scope, Guid ScopeId,
        string? ScopeCode, string? ScopeLabel,
        AssignmentRole Role, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    public record StaffWorkloadDetail(
        Guid StaffId, string StaffCode, string StaffFullName, bool StaffIsActive,
        IReadOnlyList<WorkloadRow> Clients, IReadOnlyList<WorkloadRow> ServiceFiles);

    [HttpGet("workload")]
    [HasPermission(Permissions.Staff.Read)]
    public async Task<ActionResult<IReadOnlyList<StaffWorkloadSummary>>> AllStaff(
        [FromQuery] DateOnly? asOf, CancellationToken ct)
    {
        var at = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var active = await _db.StaffAssignments.AsNoTracking()
            .Where(a => a.EffectiveFrom <= at && (a.EffectiveTo == null || a.EffectiveTo >= at))
            .Include(a => a.Staff)
            .ToListAsync(ct);

        var staffIds = active.Select(a => a.StaffId).Distinct().ToArray();
        var staffById = await _db.Staff.AsNoTracking()
            .Where(s => staffIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var summaries = staffIds
            .Select(sid =>
            {
                var mine = active.Where(a => a.StaffId == sid).ToList();
                int cnt(AssignmentScope sc, AssignmentRole r) =>
                    mine.Count(a => a.Scope == sc && a.Role == r);

                var totalClients = mine
                    .Where(a => a.Scope == AssignmentScope.Client)
                    .Select(a => a.ScopeId).Distinct().Count();
                var totalFiles = mine
                    .Where(a => a.Scope == AssignmentScope.ServiceFile)
                    .Select(a => a.ScopeId).Distinct().Count();

                var staff = staffById.GetValueOrDefault(sid);
                return new StaffWorkloadSummary(
                    sid, staff?.Code ?? "", staff?.FullName ?? "", staff?.IsActive ?? false,
                    cnt(AssignmentScope.Client, AssignmentRole.Primary),
                    cnt(AssignmentScope.Client, AssignmentRole.Secondary),
                    cnt(AssignmentScope.Client, AssignmentRole.Reviewer),
                    cnt(AssignmentScope.ServiceFile, AssignmentRole.Primary),
                    cnt(AssignmentScope.ServiceFile, AssignmentRole.Secondary),
                    cnt(AssignmentScope.ServiceFile, AssignmentRole.Reviewer),
                    totalClients, totalFiles);
            })
            .OrderBy(s => s.StaffFullName)
            .ToList();

        return summaries;
    }

    [HttpGet("staff/{staffId:guid}/workload")]
    [HasPermission(Permissions.Staff.Read)]
    public async Task<ActionResult<StaffWorkloadDetail>> ForStaff(
        Guid staffId, [FromQuery] DateOnly? asOf, [FromQuery] bool includeHistory = false, CancellationToken ct = default)
    {
        var staff = await _db.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId, ct);
        if (staff is null) return NotFound();

        var at = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var q = _db.StaffAssignments.AsNoTracking().Where(a => a.StaffId == staffId);
        if (!includeHistory)
            q = q.Where(a => a.EffectiveFrom <= at && (a.EffectiveTo == null || a.EffectiveTo >= at));
        var rows = await q.ToListAsync(ct);

        var clientIds = rows.Where(a => a.Scope == AssignmentScope.Client).Select(a => a.ScopeId).Distinct().ToArray();
        var fileIds   = rows.Where(a => a.Scope == AssignmentScope.ServiceFile).Select(a => a.ScopeId).Distinct().ToArray();

        var clientLookup = await _db.Clients.AsNoTracking()
            .Where(c => clientIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Code, c.LegalName })
            .ToDictionaryAsync(c => c.Id, ct);
        var fileLookup = await _db.ServiceFiles.AsNoTracking()
            .Where(f => fileIds.Contains(f.Id))
            .Include(f => f.ServiceDefinition)
            .Select(f => new { f.Id, f.FileNumber, Service = f.ServiceDefinition!.Name })
            .ToDictionaryAsync(f => f.Id, ct);

        WorkloadRow Project(StaffAssignment a)
        {
            string? code = null, label = null;
            if (a.Scope == AssignmentScope.Client && clientLookup.TryGetValue(a.ScopeId, out var cl))
                (code, label) = (cl.Code, cl.LegalName);
            else if (a.Scope == AssignmentScope.ServiceFile && fileLookup.TryGetValue(a.ScopeId, out var fl))
                (code, label) = (fl.FileNumber, fl.Service);
            return new WorkloadRow(a.Id, a.Scope, a.ScopeId, code, label,
                                   a.Role, a.EffectiveFrom, a.EffectiveTo);
        }

        var clientRows = rows.Where(a => a.Scope == AssignmentScope.Client)
            .OrderBy(a => a.Role).ThenByDescending(a => a.EffectiveFrom)
            .Select(Project).ToList();
        var fileRows = rows.Where(a => a.Scope == AssignmentScope.ServiceFile)
            .OrderBy(a => a.Role).ThenByDescending(a => a.EffectiveFrom)
            .Select(Project).ToList();

        return new StaffWorkloadDetail(staff.Id, staff.Code, staff.FullName, staff.IsActive, clientRows, fileRows);
    }
}
