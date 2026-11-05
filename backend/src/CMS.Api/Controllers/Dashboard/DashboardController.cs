using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly ICurrentUser _user;

    public DashboardController(CmsDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public record Kpis(
        int ActiveClients, int InactiveClients,
        int ActiveServiceFiles, int OnHoldServiceFiles, int ClosedServiceFiles,
        int OpenEngagements, int InProgressEngagements, int CompletedEngagements,
        int ActiveBranches, int ActiveStaff);

    public record Breakdown(string Label, int Count);

    [HttpGet("kpis")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<Kpis>> GetKpis(CancellationToken ct)
    {
        var activeClients = await _db.Clients.CountAsync(c => c.IsActive, ct);
        var inactiveClients = await _db.Clients.CountAsync(c => !c.IsActive, ct);
        var activeFiles = await _db.ServiceFiles.CountAsync(f => f.Status == ServiceFileStatus.Open, ct);
        var onHoldFiles = await _db.ServiceFiles.CountAsync(f => f.Status == ServiceFileStatus.OnHold, ct);
        var closedFiles = await _db.ServiceFiles.CountAsync(f => f.Status == ServiceFileStatus.Closed, ct);
        var openEng = await _db.Engagements.CountAsync(e => e.Status == EngagementStatus.Planned, ct);
        var inProgEng = await _db.Engagements.CountAsync(e => e.Status == EngagementStatus.InProgress, ct);
        var doneEng = await _db.Engagements.CountAsync(e => e.Status == EngagementStatus.Completed, ct);
        var activeBranches = await _db.Branches.CountAsync(b => b.IsActive, ct);
        var activeStaff = await _db.Staff.CountAsync(s => s.IsActive, ct);

        return new Kpis(activeClients, inactiveClients, activeFiles, onHoldFiles, closedFiles,
                        openEng, inProgEng, doneEng, activeBranches, activeStaff);
    }

    [HttpGet("clients-by-entity-type")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<IReadOnlyList<Breakdown>>> ClientsByEntityType(CancellationToken ct)
    {
        // Materialise two small projections, then group in memory — portable across providers.
        var clients = await _db.Clients.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.EntityTypeId).ToListAsync(ct);
        var names = await _db.EntityTypes.AsNoTracking()
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        return clients
            .GroupBy(id => names.GetValueOrDefault(id, "(unknown)"))
            .Select(g => new Breakdown(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Label)
            .ToList();
    }

    [HttpGet("clients-by-state")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<IReadOnlyList<Breakdown>>> ClientsByState(CancellationToken ct)
    {
        var primaryBranches = await _db.Branches.AsNoTracking()
            .Where(b => b.IsPrimary && b.IsActive)
            .Select(b => new { b.ClientId, b.StateId }).ToListAsync(ct);
        var activeClientIds = await _db.Clients.AsNoTracking()
            .Where(c => c.IsActive).Select(c => c.Id).ToListAsync(ct);
        var stateNames = await _db.States.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var activeSet = activeClientIds.ToHashSet();
        return primaryBranches
            .Where(b => activeSet.Contains(b.ClientId))
            .GroupBy(b => stateNames.GetValueOrDefault(b.StateId, "(unknown)"))
            .Select(g => new Breakdown(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Label)
            .ToList();
    }

    [HttpGet("service-files-by-service")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<IReadOnlyList<Breakdown>>> ServiceFilesByService(CancellationToken ct)
    {
        var files = await _db.ServiceFiles.AsNoTracking()
            .Where(f => f.Status != ServiceFileStatus.Closed)
            .Select(f => f.ServiceDefinitionId).ToListAsync(ct);
        var svcNames = await _db.ServiceDefinitions.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return files
            .GroupBy(id => svcNames.GetValueOrDefault(id, "(unknown)"))
            .Select(g => new Breakdown(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Label)
            .ToList();
    }

    [HttpGet("staff-workload-top")]
    public async Task<ActionResult<IReadOnlyList<Breakdown>>> StaffWorkloadTop(
        [FromQuery] int take = 10, CancellationToken ct = default)
    {
        if (!_user.HasPermission(Permissions.Staff.Read))
            return Forbid();
        if (take <= 0 || take > 50) take = 10;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var current = await _db.StaffAssignments.AsNoTracking()
            .Where(a => a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .Select(a => a.StaffId).ToListAsync(ct);
        var staffNames = await _db.Staff.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.FullName, ct);

        return current
            .GroupBy(sid => staffNames.GetValueOrDefault(sid, "(unknown)"))
            .Select(g => new Breakdown(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Label)
            .Take(take)
            .ToList();
    }
}
