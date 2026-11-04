using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers.Assignments;

/// <summary>
/// Encapsulates the append-only reassignment rule shared by client-level and file-level controllers.
/// On assign: at most one open row is closed by stamping EffectiveTo = new.EffectiveFrom - 1 day;
/// the new row is inserted. Historical rows are never rewritten in place.
/// </summary>
public class StaffAssignmentService
{
    private readonly CmsDbContext _db;
    public StaffAssignmentService(CmsDbContext db) => _db = db;

    public record AssignmentDto(
        Guid Id, AssignmentScope Scope, Guid ScopeId,
        Guid StaffId, string? StaffCode, string? StaffFullName,
        AssignmentRole Role, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
        string? Notes, int Version);

    public static AssignmentDto Map(StaffAssignment a) => new(
        a.Id, a.Scope, a.ScopeId,
        a.StaffId, a.Staff?.Code, a.Staff?.FullName,
        a.Role, a.EffectiveFrom, a.EffectiveTo, a.Notes, a.Version);

    public async Task<ActionResult<IReadOnlyList<AssignmentDto>>> ListAsync(
        AssignmentScope scope, Guid scopeId, DateOnly? asOf, bool includeHistory, AssignmentRole? role, CancellationToken ct)
    {
        var q = _db.StaffAssignments.AsNoTracking().Include(a => a.Staff)
            .Where(a => a.Scope == scope && a.ScopeId == scopeId);
        if (role is not null) q = q.Where(a => a.Role == role);
        if (!includeHistory)
        {
            var at = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
            q = q.Where(a => a.EffectiveFrom <= at && (a.EffectiveTo == null || a.EffectiveTo >= at));
        }
        var rows = await q.OrderBy(a => a.Role).ThenByDescending(a => a.EffectiveFrom).ToListAsync(ct);
        return new OkObjectResult(rows.Select(Map).ToList());
    }

    public async Task<ActionResult<AssignmentDto>> AssignAsync(
        AssignmentScope scope, Guid scopeId, Guid staffId, AssignmentRole role,
        DateOnly effectiveFrom, string? notes, CancellationToken ct)
    {
        if (!await _db.Staff.AnyAsync(s => s.Id == staffId && s.IsActive, ct))
            return new BadRequestObjectResult(new { error = "invalid_or_inactive_staff" });

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Find currently-open rows that conflict with the new assignment.
        // Primary and Reviewer: at most one current row per scope.
        // Secondary: only conflicts if the SAME staff is already current under Secondary.
        var openForScopeRole = await _db.StaffAssignments
            .Where(a => a.Scope == scope && a.ScopeId == scopeId && a.Role == role && a.EffectiveTo == null)
            .ToListAsync(ct);

        if (role == AssignmentRole.Secondary)
        {
            if (openForScopeRole.Any(a => a.StaffId == staffId))
                return new ConflictObjectResult(new { error = "already_assigned" });
        }
        else
        {
            var current = openForScopeRole.FirstOrDefault();
            if (current is not null)
            {
                if (current.StaffId == staffId)
                    return new ConflictObjectResult(new { error = "already_assigned" });
                var closeOn = effectiveFrom.AddDays(-1);
                if (closeOn < current.EffectiveFrom) closeOn = current.EffectiveFrom;
                current.End(closeOn);
            }
        }

        var row = new StaffAssignment(scope, scopeId, staffId, role, effectiveFrom, notes);
        _db.StaffAssignments.Add(row);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _db.Entry(row).Reference(a => a.Staff).LoadAsync(ct);
        return new OkObjectResult(Map(row));
    }

    public async Task<ActionResult<AssignmentDto>> EndAsync(
        AssignmentScope scope, Guid scopeId, Guid id, int version, DateOnly? effectiveTo, CancellationToken ct)
    {
        var row = await _db.StaffAssignments.Include(a => a.Staff)
            .FirstOrDefaultAsync(a => a.Scope == scope && a.ScopeId == scopeId && a.Id == id, ct);
        if (row is null) return new NotFoundResult();

        try
        {
            _db.Entry(row).Property(a => a.Version).OriginalValue = version;
            row.End(effectiveTo ?? DateOnly.FromDateTime(DateTime.UtcNow));
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex) { return new BadRequestObjectResult(new { error = "invalid_state", detail = ex.Message }); }
        catch (ArgumentException ex) { return new BadRequestObjectResult(new { error = "invalid_input", detail = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return new ConflictObjectResult(new { error = "concurrency_conflict" }); }

        return new OkObjectResult(Map(row));
    }
}
