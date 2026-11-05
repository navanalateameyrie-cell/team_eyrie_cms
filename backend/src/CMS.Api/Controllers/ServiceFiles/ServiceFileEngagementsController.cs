using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Application.Clients;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/clients/{clientId:guid}/service-files/{fileId:guid}/engagements")]
public class ServiceFileEngagementsController : ControllerBase
{
    private readonly CmsDbContext _db;
    public ServiceFileEngagementsController(CmsDbContext db) => _db = db;

    public record EngagementDto(
        Guid Id, Guid ServiceFileId,
        string FinancialYear, string? AssessmentYear,
        EngagementPeriodType PeriodType, string? PeriodLabel,
        DateOnly PlannedStartDate, DateOnly PlannedEndDate,
        DateOnly? ActualStartDate, DateOnly? ActualEndDate,
        EngagementStatus Status, string? Notes, int Version);

    public record CreateEngagementRequest(
        [Required, StringLength(7, MinimumLength = 7)] string FinancialYear,
        [StringLength(7, MinimumLength = 7)] string? AssessmentYear,
        [Required] EngagementPeriodType PeriodType,
        [StringLength(32)] string? PeriodLabel,
        [Required] DateOnly PlannedStartDate,
        [Required] DateOnly PlannedEndDate,
        [StringLength(2000)] string? Notes);

    public record UpdateEngagementRequest(
        [Required, StringLength(7, MinimumLength = 7)] string FinancialYear,
        [StringLength(7, MinimumLength = 7)] string? AssessmentYear,
        [Required] EngagementPeriodType PeriodType,
        [StringLength(32)] string? PeriodLabel,
        [Required] DateOnly PlannedStartDate,
        [Required] DateOnly PlannedEndDate,
        [StringLength(2000)] string? Notes,
        [Required] int Version);

    private static EngagementDto Map(Engagement e) => new(
        e.Id, e.ServiceFileId, e.FinancialYear, e.AssessmentYear,
        e.PeriodType, e.PeriodLabel, e.PlannedStartDate, e.PlannedEndDate,
        e.ActualStartDate, e.ActualEndDate, e.Status, e.Notes, e.Version);

    private async Task<ServiceFile?> FileAsync(Guid clientId, Guid fileId, CancellationToken ct) =>
        await _db.ServiceFiles.FirstOrDefaultAsync(f => f.ClientId == clientId && f.Id == fileId, ct);

    [HttpGet]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<IReadOnlyList<EngagementDto>>> List(
        Guid clientId, Guid fileId,
        [FromQuery] string? financialYear,
        [FromQuery] EngagementStatus? status,
        CancellationToken ct)
    {
        if (await FileAsync(clientId, fileId, ct) is null) return NotFound();
        var q = _db.Engagements.AsNoTracking().Where(e => e.ServiceFileId == fileId);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(e => e.FinancialYear == financialYear);
        if (status is not null) q = q.Where(e => e.Status == status);
        var rows = await q
            .OrderByDescending(e => e.FinancialYear)
            .ThenBy(e => e.PeriodType).ThenBy(e => e.PeriodLabel)
            .ToListAsync(ct);
        return Ok(rows.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Clients.Read)]
    public async Task<ActionResult<EngagementDto>> Get(Guid clientId, Guid fileId, Guid id, CancellationToken ct)
    {
        if (await FileAsync(clientId, fileId, ct) is null) return NotFound();
        var row = await _db.Engagements.AsNoTracking().FirstOrDefaultAsync(e => e.ServiceFileId == fileId && e.Id == id, ct);
        return row is null ? NotFound() : Map(row);
    }

    [HttpPost]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<EngagementDto>> Create(Guid clientId, Guid fileId,
        [FromBody] CreateEngagementRequest req, CancellationToken ct)
    {
        var file = await FileAsync(clientId, fileId, ct);
        if (file is null) return NotFound();
        if (file.Status == ServiceFileStatus.Closed)
            return BadRequest(new { error = "service_file_closed", hint = "Reopen the file to add new engagements." });

        string fy;
        string? ay;
        try
        {
            fy = FinancialYearValidator.NormalizeOrThrow(req.FinancialYear);
            ay = FinancialYearValidator.NormalizeAssessmentYear(req.AssessmentYear);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "invalid_financial_year", detail = ex.Message });
        }

        var effectiveLabel = req.PeriodType == EngagementPeriodType.Annual ? null : req.PeriodLabel;
        var exists = await _db.Engagements.AnyAsync(e =>
            e.ServiceFileId == fileId &&
            e.FinancialYear == fy &&
            e.PeriodType == req.PeriodType &&
            e.PeriodLabel == effectiveLabel, ct);
        if (exists)
            return Conflict(new { error = "duplicate_engagement_period",
                                  hint = "An engagement for this (year, period) already exists for this file." });

        Engagement engagement;
        try
        {
            engagement = new Engagement(fileId, fy, ay, req.PeriodType, req.PeriodLabel,
                                        req.PlannedStartDate, req.PlannedEndDate, req.Notes);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = "invalid_input", detail = ex.Message }); }

        _db.Engagements.Add(engagement);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { clientId, fileId, id = engagement.Id, version = "1.0" }, Map(engagement));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<EngagementDto>> Update(Guid clientId, Guid fileId, Guid id,
        [FromBody] UpdateEngagementRequest req, CancellationToken ct)
    {
        if (await FileAsync(clientId, fileId, ct) is null) return NotFound();
        var row = await _db.Engagements.FirstOrDefaultAsync(e => e.ServiceFileId == fileId && e.Id == id, ct);
        if (row is null) return NotFound();

        string fy;
        string? ay;
        try
        {
            fy = FinancialYearValidator.NormalizeOrThrow(req.FinancialYear);
            ay = FinancialYearValidator.NormalizeAssessmentYear(req.AssessmentYear);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = "invalid_financial_year", detail = ex.Message }); }

        var effectiveLabel = req.PeriodType == EngagementPeriodType.Annual ? null : req.PeriodLabel;
        var clash = await _db.Engagements.AnyAsync(e =>
            e.ServiceFileId == fileId &&
            e.Id != id &&
            e.FinancialYear == fy &&
            e.PeriodType == req.PeriodType &&
            e.PeriodLabel == effectiveLabel, ct);
        if (clash) return Conflict(new { error = "duplicate_engagement_period" });

        try
        {
            _db.Entry(row).Property(e => e.Version).OriginalValue = req.Version;
            row.UpdateProfile(fy, ay, req.PeriodType, req.PeriodLabel,
                              req.PlannedStartDate, req.PlannedEndDate, req.Notes);
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "invalid_state", detail = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = "invalid_input", detail = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }

        return Map(row);
    }

    [HttpPost("{id:guid}/start")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<EngagementDto>> Start(Guid clientId, Guid fileId, Guid id,
        [FromQuery, Required] int version, [FromQuery] DateOnly? actualStart, CancellationToken ct)
    {
        if (await FileAsync(clientId, fileId, ct) is null) return NotFound();
        var row = await _db.Engagements.FirstOrDefaultAsync(e => e.ServiceFileId == fileId && e.Id == id, ct);
        if (row is null) return NotFound();
        try
        {
            _db.Entry(row).Property(e => e.Version).OriginalValue = version;
            row.Start(actualStart ?? DateOnly.FromDateTime(DateTime.UtcNow));
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "invalid_transition", detail = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }
        return Map(row);
    }

    [HttpPost("{id:guid}/complete")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<EngagementDto>> Complete(Guid clientId, Guid fileId, Guid id,
        [FromQuery, Required] int version, [FromQuery] DateOnly? actualEnd, CancellationToken ct)
    {
        if (await FileAsync(clientId, fileId, ct) is null) return NotFound();
        var row = await _db.Engagements.FirstOrDefaultAsync(e => e.ServiceFileId == fileId && e.Id == id, ct);
        if (row is null) return NotFound();
        try
        {
            _db.Entry(row).Property(e => e.Version).OriginalValue = version;
            row.Complete(actualEnd ?? DateOnly.FromDateTime(DateTime.UtcNow));
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "invalid_transition", detail = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = "invalid_input", detail = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }
        return Map(row);
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.Clients.Write)]
    public async Task<ActionResult<EngagementDto>> Cancel(Guid clientId, Guid fileId, Guid id,
        [FromQuery, Required] int version, CancellationToken ct)
    {
        if (await FileAsync(clientId, fileId, ct) is null) return NotFound();
        var row = await _db.Engagements.FirstOrDefaultAsync(e => e.ServiceFileId == fileId && e.Id == id, ct);
        if (row is null) return NotFound();
        try
        {
            _db.Entry(row).Property(e => e.Version).OriginalValue = version;
            row.Cancel();
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "invalid_transition", detail = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "concurrency_conflict" }); }
        return Map(row);
    }
}
