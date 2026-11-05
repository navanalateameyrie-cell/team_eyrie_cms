using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Abstractions;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Compliance;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance")]
public class ComplianceController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IComplianceObligationGenerator _generator;

    public ComplianceController(CmsDbContext db, ICurrentUser user,
                                 IComplianceObligationGenerator generator)
    {
        _db = db;
        _user = user;
        _generator = generator;
    }

    public record ObligationDto(
        Guid Id, Guid ServiceFileId, string FileNumber,
        Guid ClientId, string ClientCode, string ClientName,
        Guid ServiceDefinitionId, string ServiceCode, string ServiceName,
        string PeriodKey, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly DueDate,
        ComplianceStatus Status, Guid? AssignedStaffId, string? AssignedStaffName,
        DateOnly? FiledOn, string? Reference, string? Notes, int Version);

    public record EventDto(
        Guid Id, ObligationEventKind Kind,
        ComplianceStatus? FromStatus, ComplianceStatus? ToStatus,
        Guid? StaffId, string? Note, string? ActorUserName, DateTimeOffset OccurredAtUtc);

    public record Page<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take);

    public record Aggregates(int DueThisWeek, int OverdueOpen, int FiledThisMonth, int InProgress);

    // ---------------- List ----------------

    [HttpGet]
    [HasPermission(Permissions.Compliance.Read)]
    public async Task<ActionResult<Page<ObligationDto>>> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] ComplianceStatus? status,
        [FromQuery] Guid? assigneeId, [FromQuery] Guid? clientId, [FromQuery] Guid? branchId,
        [FromQuery] string? serviceCode,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (take <= 0 || take > 500) take = 50;
        if (skip < 0) skip = 0;

        var q = _db.ComplianceObligations.AsNoTracking().AsQueryable();
        if (from is not null) q = q.Where(o => o.DueDate >= from);
        if (to is not null) q = q.Where(o => o.DueDate <= to);
        if (status is not null) q = q.Where(o => o.Status == status);
        if (assigneeId is not null) q = q.Where(o => o.AssignedStaffId == assigneeId);

        // Join to service_files / services / clients for filtering + projection.
        var baseQuery =
            from o in q
            join f in _db.ServiceFiles.AsNoTracking() on o.ServiceFileId equals f.Id
            join c in _db.Clients.AsNoTracking() on f.ClientId equals c.Id
            join s in _db.ServiceDefinitions.AsNoTracking() on f.ServiceDefinitionId equals s.Id
            select new { o, f, c, s };

        if (clientId is not null) baseQuery = baseQuery.Where(x => x.c.Id == clientId);
        if (branchId is not null) baseQuery = baseQuery.Where(x => x.f.BranchId == branchId);
        if (!string.IsNullOrWhiteSpace(serviceCode))
        {
            var code = serviceCode.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(x => x.s.Code == code);
        }

        var total = await baseQuery.CountAsync(ct);
        var rows = await baseQuery
            .OrderBy(x => x.o.DueDate).ThenBy(x => x.c.LegalName)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        var staffIds = rows.Where(r => r.o.AssignedStaffId.HasValue)
            .Select(r => r.o.AssignedStaffId!.Value).Distinct().ToArray();
        var staffNames = await _db.Staff.AsNoTracking()
            .Where(s => staffIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.FullName, ct);

        var items = rows.Select(r => new ObligationDto(
            r.o.Id, r.o.ServiceFileId, r.f.FileNumber,
            r.c.Id, r.c.Code, r.c.LegalName,
            r.s.Id, r.s.Code, r.s.Name,
            r.o.PeriodKey, r.o.PeriodStart, r.o.PeriodEnd, r.o.DueDate,
            r.o.Status, r.o.AssignedStaffId,
            r.o.AssignedStaffId.HasValue ? staffNames.GetValueOrDefault(r.o.AssignedStaffId.Value) : null,
            r.o.FiledOn, r.o.Reference, r.o.Notes, r.o.Version
        )).ToList();

        return new Page<ObligationDto>(items, total, skip, take);
    }

    // ---------------- Dashboard aggregates ----------------

    [HttpGet("aggregates")]
    [HasPermission(Permissions.Compliance.Read)]
    public async Task<ActionResult<Aggregates>> GetAggregates(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekEnd = today.AddDays(7);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var dueThisWeek = await _db.ComplianceObligations.AsNoTracking()
            .CountAsync(o => o.DueDate >= today && o.DueDate <= weekEnd
                             && o.Status != ComplianceStatus.Filed && o.Status != ComplianceStatus.Waived, ct);
        var overdueOpen = await _db.ComplianceObligations.AsNoTracking()
            .CountAsync(o => o.DueDate < today
                             && o.Status != ComplianceStatus.Filed && o.Status != ComplianceStatus.Waived, ct);
        var filedThisMonth = await _db.ComplianceObligations.AsNoTracking()
            .CountAsync(o => o.Status == ComplianceStatus.Filed
                             && o.FiledOn != null && o.FiledOn >= monthStart, ct);
        var inProgress = await _db.ComplianceObligations.AsNoTracking()
            .CountAsync(o => o.Status == ComplianceStatus.InProgress, ct);

        return new Aggregates(dueThisWeek, overdueOpen, filedThisMonth, inProgress);
    }

    // ---------------- Single + timeline ----------------

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Compliance.Read)]
    public async Task<ActionResult<ObligationDto>> Get(Guid id, CancellationToken ct)
    {
        var row = await (from o in _db.ComplianceObligations.AsNoTracking()
                         where o.Id == id
                         join f in _db.ServiceFiles.AsNoTracking() on o.ServiceFileId equals f.Id
                         join c in _db.Clients.AsNoTracking() on f.ClientId equals c.Id
                         join s in _db.ServiceDefinitions.AsNoTracking() on f.ServiceDefinitionId equals s.Id
                         select new { o, f, c, s }).FirstOrDefaultAsync(ct);
        if (row is null) return NotFound();
        string? staffName = null;
        if (row.o.AssignedStaffId is Guid sid)
            staffName = await _db.Staff.AsNoTracking().Where(s => s.Id == sid).Select(s => s.FullName).FirstOrDefaultAsync(ct);

        return new ObligationDto(row.o.Id, row.o.ServiceFileId, row.f.FileNumber,
            row.c.Id, row.c.Code, row.c.LegalName,
            row.s.Id, row.s.Code, row.s.Name,
            row.o.PeriodKey, row.o.PeriodStart, row.o.PeriodEnd, row.o.DueDate,
            row.o.Status, row.o.AssignedStaffId, staffName,
            row.o.FiledOn, row.o.Reference, row.o.Notes, row.o.Version);
    }

    [HttpGet("{id:guid}/events")]
    [HasPermission(Permissions.Compliance.Read)]
    public async Task<ActionResult<IReadOnlyList<EventDto>>> GetEvents(Guid id, CancellationToken ct)
    {
        var baseQuery = _db.ObligationEvents.AsNoTracking().Where(e => e.ObligationId == id);
        if (_db.Database.IsPostgres())
        {
            return await baseQuery.OrderBy(e => e.CreatedAtUtc)
                .Select(e => new EventDto(e.Id, e.Kind, e.FromStatus, e.ToStatus,
                    e.StaffId, e.Note, e.ActorUserName, e.CreatedAtUtc))
                .ToListAsync(ct);
        }
        // SQLite fallback — can't ORDER BY DateTimeOffset in SQL.
        var events = await baseQuery
            .Select(e => new EventDto(e.Id, e.Kind, e.FromStatus, e.ToStatus,
                e.StaffId, e.Note, e.ActorUserName, e.CreatedAtUtc))
            .ToListAsync(ct);
        return events.OrderBy(e => e.OccurredAtUtc).ToList();
    }

    // ---------------- Service-file scoped list ----------------

    [HttpGet("/api/v{version:apiVersion}/service-files/{fileId:guid}/obligations")]
    [HasPermission(Permissions.Compliance.Read)]
    public async Task<ActionResult<IReadOnlyList<ObligationDto>>> ListForFile(Guid fileId, CancellationToken ct)
    {
        var rows = await (from o in _db.ComplianceObligations.AsNoTracking()
                          where o.ServiceFileId == fileId
                          join f in _db.ServiceFiles.AsNoTracking() on o.ServiceFileId equals f.Id
                          join c in _db.Clients.AsNoTracking() on f.ClientId equals c.Id
                          join s in _db.ServiceDefinitions.AsNoTracking() on f.ServiceDefinitionId equals s.Id
                          orderby o.PeriodStart descending
                          select new { o, f, c, s }).ToListAsync(ct);

        var staffIds = rows.Where(r => r.o.AssignedStaffId.HasValue).Select(r => r.o.AssignedStaffId!.Value).Distinct().ToArray();
        var staffNames = await _db.Staff.AsNoTracking()
            .Where(s => staffIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.FullName, ct);

        return rows.Select(r => new ObligationDto(
            r.o.Id, r.o.ServiceFileId, r.f.FileNumber,
            r.c.Id, r.c.Code, r.c.LegalName,
            r.s.Id, r.s.Code, r.s.Name,
            r.o.PeriodKey, r.o.PeriodStart, r.o.PeriodEnd, r.o.DueDate,
            r.o.Status, r.o.AssignedStaffId,
            r.o.AssignedStaffId.HasValue ? staffNames.GetValueOrDefault(r.o.AssignedStaffId.Value) : null,
            r.o.FiledOn, r.o.Reference, r.o.Notes, r.o.Version)).ToList();
    }

    // ---------------- Mutations ----------------

    public record ObligationAssignRequest(Guid? StaffId, string? Note);
    public record FileRequest(DateOnly FiledOn, string? Reference, string? Note);
    public record StatusRequest(ComplianceStatus Status, string? Note);
    public record DueDateRequest(DateOnly DueDate, string? Note);
    public record NoteRequest(string Note);

    [HttpPost("{id:guid}/assign")]
    [HasPermission(Permissions.Compliance.Write)]
    public async Task<ActionResult<ObligationDto>> Assign(Guid id, [FromBody] ObligationAssignRequest req, CancellationToken ct)
    {
        var row = await _db.ComplianceObligations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return NotFound();

        if (req.StaffId is Guid sid && !await _db.Staff.AnyAsync(s => s.Id == sid && s.IsActive, ct))
            return UnprocessableEntity(new { error = "unknown_or_inactive_staff" });

        var from = row.Status;
        row.Assign(req.StaffId);
        _db.ObligationEvents.Add(new ObligationEvent(row.Id, ObligationEventKind.Reassigned,
            fromStatus: from, toStatus: from, staffId: req.StaffId,
            note: req.Note, actorUserId: _user.UserId, actorUserName: _user.UserName));
        await _db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpPost("{id:guid}/status")]
    [HasPermission(Permissions.Compliance.Write)]
    public async Task<ActionResult<ObligationDto>> ChangeStatus(Guid id, [FromBody] StatusRequest req, CancellationToken ct)
    {
        var row = await _db.ComplianceObligations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return NotFound();

        var from = row.Status;
        try
        {
            switch (req.Status)
            {
                case ComplianceStatus.InProgress: row.MarkInProgress(); break;
                case ComplianceStatus.Delayed: row.MarkDelayed(); break;
                case ComplianceStatus.Waived: row.Waive(req.Note); break;
                case ComplianceStatus.Pending:
                case ComplianceStatus.Filed:
                    return UnprocessableEntity(new { error = "use_dedicated_endpoint",
                        hint = req.Status == ComplianceStatus.Filed ? "POST /file" : "status change not supported" });
                default: return UnprocessableEntity(new { error = "unknown_status" });
            }
        }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "invalid_transition", detail = ex.Message }); }

        _db.ObligationEvents.Add(new ObligationEvent(row.Id, ObligationEventKind.StatusChanged,
            fromStatus: from, toStatus: row.Status, staffId: null,
            note: req.Note, actorUserId: _user.UserId, actorUserName: _user.UserName));
        await _db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpPost("{id:guid}/file")]
    [HasPermission(Permissions.Compliance.Write)]
    public async Task<ActionResult<ObligationDto>> MarkFiled(Guid id, [FromBody] FileRequest req, CancellationToken ct)
    {
        var row = await _db.ComplianceObligations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return NotFound();

        var from = row.Status;
        try { row.MarkFiled(req.FiledOn, req.Reference); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = "invalid_transition", detail = ex.Message }); }

        _db.ObligationEvents.Add(new ObligationEvent(row.Id, ObligationEventKind.StatusChanged,
            fromStatus: from, toStatus: ComplianceStatus.Filed, staffId: null,
            note: string.IsNullOrWhiteSpace(req.Note) ? $"Filed; ref={req.Reference ?? "n/a"}" : req.Note,
            actorUserId: _user.UserId, actorUserName: _user.UserName));
        await _db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpPost("{id:guid}/due-date")]
    [HasPermission(Permissions.Compliance.Write)]
    public async Task<ActionResult<ObligationDto>> ChangeDueDate(Guid id, [FromBody] DueDateRequest req, CancellationToken ct)
    {
        var row = await _db.ComplianceObligations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return NotFound();
        row.SetDueDate(req.DueDate);
        _db.ObligationEvents.Add(new ObligationEvent(row.Id, ObligationEventKind.DueDateChanged,
            fromStatus: row.Status, toStatus: row.Status, staffId: null,
            note: req.Note ?? $"Due date set to {req.DueDate:yyyy-MM-dd}",
            actorUserId: _user.UserId, actorUserName: _user.UserName));
        await _db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpPost("{id:guid}/notes")]
    [HasPermission(Permissions.Compliance.Write)]
    public async Task<ActionResult<ObligationDto>> AddNote(Guid id, [FromBody] NoteRequest req, CancellationToken ct)
    {
        var row = await _db.ComplianceObligations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (row is null) return NotFound();
        row.SetNote(req.Note);
        _db.ObligationEvents.Add(new ObligationEvent(row.Id, ObligationEventKind.Noted,
            fromStatus: row.Status, toStatus: row.Status, staffId: null,
            note: req.Note, actorUserId: _user.UserId, actorUserName: _user.UserName));
        await _db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    // ---------------- Manual generation trigger ----------------

    public record GenerateRequest(int HorizonMonths = 6);
    public record GenerateResponse(int FilesScanned, int ObligationsCreated, int ObligationsSkippedExisting);

    [HttpPost("generate")]
    [HasPermission(Permissions.Compliance.Write)]
    public async Task<ActionResult<GenerateResponse>> Generate([FromBody] GenerateRequest? req, CancellationToken ct)
    {
        var horizon = req?.HorizonMonths ?? 6;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await _generator.GenerateAsync(today, horizon, _user.UserId, _user.UserName, ct);
        return new GenerateResponse(result.FilesScanned, result.ObligationsCreated, result.ObligationsSkippedExisting);
    }

    // ---------------- Manual reminder sweep ----------------

    public record ReminderRunDto(int ScannedObligations, int Assignees,
                                  int Upcoming, int Overdue, int EmailsSent, int EmailsSkipped, bool DryRun);

    [HttpPost("reminders/run")]
    [HasPermission(Permissions.Compliance.Write)]
    public async Task<ActionResult<ReminderRunDto>> RunReminders(
        [FromServices] IComplianceReminderService reminders,
        [FromQuery] int? windowDays, [FromQuery] bool dryRun = false,
        CancellationToken ct = default)
    {
        var r = await reminders.RunAsync(windowDays, dryRun, ct);
        return new ReminderRunDto(r.ScannedObligations, r.Assignees, r.Upcoming, r.Overdue,
            r.EmailsSent, r.EmailsSkipped, r.DryRun);
    }
}
