using System.ComponentModel.DataAnnotations;
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
[Route("api/v{version:apiVersion}/timesheet")]
public class TimesheetController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly ICurrentUser _me;
    public TimesheetController(CmsDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public record TimeEntryDto(Guid Id, Guid StaffId, string StaffName,
                                Guid EngagementId, string ClientName, string ServiceCode,
                                string FinancialYear, string? PeriodLabel,
                                DateOnly WorkDate, int Minutes, bool Billable,
                                string? Notes, bool IsApproved, int Version);

    public record CreateTimeEntryRequest(
        [Required] Guid StaffId,
        [Required] Guid EngagementId,
        [Required] DateOnly WorkDate,
        [Range(1, 1440)] int Minutes,
        bool Billable,
        [StringLength(2000)] string? Notes);

    public record UpdateTimeEntryRequest(
        [Range(1, 1440)] int Minutes,
        bool Billable,
        [StringLength(2000)] string? Notes);

    [HttpGet]
    [HasPermission(Permissions.Timesheet.Read)]
    public async Task<ActionResult<IReadOnlyList<TimeEntryDto>>> List(
        [FromQuery] Guid? staffId, [FromQuery] DateOnly? weekStart,
        CancellationToken ct)
    {
        var start = weekStart ?? MondayOf(DateOnly.FromDateTime(DateTime.UtcNow));
        var end = start.AddDays(7);

        var q = _db.TimeEntries.AsNoTracking().AsQueryable()
            .Where(t => t.WorkDate >= start && t.WorkDate < end);
        if (staffId is not null) q = q.Where(t => t.StaffId == staffId);
        else if (_me.StaffId is Guid mine) q = q.Where(t => t.StaffId == mine);

        return await (from t in q
                      join s in _db.Staff.AsNoTracking() on t.StaffId equals s.Id
                      join e in _db.Engagements.AsNoTracking() on t.EngagementId equals e.Id
                      join f in _db.ServiceFiles.AsNoTracking() on e.ServiceFileId equals f.Id
                      join c in _db.Clients.AsNoTracking() on f.ClientId equals c.Id
                      join sd in _db.ServiceDefinitions.AsNoTracking() on f.ServiceDefinitionId equals sd.Id
                      orderby t.WorkDate, c.LegalName
                      select new TimeEntryDto(t.Id, t.StaffId, s.FullName,
                          e.Id, c.LegalName, sd.Code,
                          e.FinancialYear, e.PeriodLabel,
                          t.WorkDate, t.Minutes, t.Billable,
                          t.Notes, t.IsApproved, t.Version)).ToListAsync(ct);
    }

    [HttpPost]
    [HasPermission(Permissions.Timesheet.Write)]
    public async Task<ActionResult<TimeEntryDto>> Create([FromBody] CreateTimeEntryRequest req, CancellationToken ct)
    {
        if (!await _db.Staff.AnyAsync(s => s.Id == req.StaffId && s.IsActive, ct))
            return UnprocessableEntity(new { error = "unknown_or_inactive_staff" });
        if (!await _db.Engagements.AnyAsync(e => e.Id == req.EngagementId, ct))
            return UnprocessableEntity(new { error = "unknown_engagement" });

        // Non-approvers can only log their own time — approvers may enter time on behalf of staff.
        if (!_me.HasPermission(Permissions.Timesheet.Approve) && _me.StaffId is Guid mine && req.StaffId != mine)
            return Forbid();

        var entry = new TimeEntry(req.StaffId, req.EngagementId, req.WorkDate,
            req.Minutes, req.Billable, req.Notes);
        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { version = "1.0" },
            await LoadOneAsync(entry.Id, ct));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Timesheet.Write)]
    public async Task<ActionResult<TimeEntryDto>> Update(Guid id, [FromBody] UpdateTimeEntryRequest req, CancellationToken ct)
    {
        var entry = await _db.TimeEntries.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entry is null) return NotFound();
        if (!_me.HasPermission(Permissions.Timesheet.Approve) && _me.StaffId is Guid mine && entry.StaffId != mine)
            return Forbid();
        try { entry.Edit(req.Minutes, req.Billable, req.Notes); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "approved_locked", detail = ex.Message }); }
        await _db.SaveChangesAsync(ct);
        return await LoadOneAsync(id, ct);
    }

    [HttpPost("{id:guid}/approve")]
    [HasPermission(Permissions.Timesheet.Approve)]
    public async Task<ActionResult<TimeEntryDto>> Approve(Guid id, CancellationToken ct)
    {
        var entry = await _db.TimeEntries.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entry is null) return NotFound();
        entry.Approve(_me.UserId ?? Guid.Empty);
        await _db.SaveChangesAsync(ct);
        return await LoadOneAsync(id, ct);
    }

    [HttpPost("{id:guid}/unapprove")]
    [HasPermission(Permissions.Timesheet.Approve)]
    public async Task<ActionResult<TimeEntryDto>> Unapprove(Guid id, CancellationToken ct)
    {
        var entry = await _db.TimeEntries.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entry is null) return NotFound();
        entry.Unapprove();
        await _db.SaveChangesAsync(ct);
        return await LoadOneAsync(id, ct);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Timesheet.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entry = await _db.TimeEntries.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entry is null) return NotFound();
        if (entry.IsApproved) return UnprocessableEntity(new { error = "approved_locked" });
        if (!_me.HasPermission(Permissions.Timesheet.Approve) && _me.StaffId is Guid mine && entry.StaffId != mine)
            return Forbid();
        _db.TimeEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<TimeEntryDto> LoadOneAsync(Guid id, CancellationToken ct)
    {
        return await (from t in _db.TimeEntries.AsNoTracking()
                      where t.Id == id
                      join s in _db.Staff.AsNoTracking() on t.StaffId equals s.Id
                      join e in _db.Engagements.AsNoTracking() on t.EngagementId equals e.Id
                      join f in _db.ServiceFiles.AsNoTracking() on e.ServiceFileId equals f.Id
                      join c in _db.Clients.AsNoTracking() on f.ClientId equals c.Id
                      join sd in _db.ServiceDefinitions.AsNoTracking() on f.ServiceDefinitionId equals sd.Id
                      select new TimeEntryDto(t.Id, t.StaffId, s.FullName,
                          e.Id, c.LegalName, sd.Code,
                          e.FinancialYear, e.PeriodLabel,
                          t.WorkDate, t.Minutes, t.Billable,
                          t.Notes, t.IsApproved, t.Version)).FirstAsync(ct);
    }

    private static DateOnly MondayOf(DateOnly d)
    {
        var dow = (int)d.DayOfWeek;
        var deltaToMonday = (dow == 0 ? 6 : dow - 1); // Sunday → 6 days back, otherwise N-1.
        return d.AddDays(-deltaToMonday);
    }
}
