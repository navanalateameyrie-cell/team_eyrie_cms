using System.ComponentModel.DataAnnotations;
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
[Route("api/v{version:apiVersion}/clients/{clientId:guid}/tds")]
public class TdsController : ControllerBase
{
    private readonly CmsDbContext _db;
    public TdsController(CmsDbContext db) => _db = db;

    // ---------- DTOs ----------

    public record TdsEntryDto(Guid Id, Guid ClientId, DateOnly DeductionDate,
        string Section, string DeducteeName, string? DeducteePan,
        decimal GrossAmountInr, decimal TdsRatePercent, decimal TdsAmountInr,
        TdsQuarter Quarter, string FinancialYear,
        Guid? ChallanId, string? ChallanNumber, string? Notes, int Version);

    public record TdsEntryUpsertRequest(
        [Required] DateOnly DeductionDate,
        [Required, StringLength(16)] string Section,
        [Required, StringLength(300)] string DeducteeName,
        [StringLength(10, MinimumLength = 10)] string? DeducteePan,
        [Range(0, 100_000_000)] decimal GrossAmountInr,
        [Range(0, 100)] decimal TdsRatePercent,
        [StringLength(2000)] string? Notes);

    public record TdsChallanDto(Guid Id, Guid ClientId, string ChallanNumber,
        DateOnly DepositDate, string? BankBsrCode, string Section,
        decimal TotalAmountInr, string? Notes, int Version);

    public record TdsChallanUpsertRequest(
        [Required, StringLength(64)] string ChallanNumber,
        [Required] DateOnly DepositDate,
        [StringLength(32)] string? BankBsrCode,
        [Required, StringLength(16)] string Section,
        [Range(0, 10_000_000_000d)] decimal TotalAmountInr,
        [StringLength(2000)] string? Notes);

    public record LinkRequest([Required] List<Guid> EntryIds);

    public record SectionQuarterCell(string Section, TdsQuarter Quarter,
        int Count, decimal GrossInr, decimal TdsInr, decimal DepositedInr);

    public record Summary(string FinancialYear,
        decimal GrossInr, decimal TdsInr, decimal DepositedInr,
        IReadOnlyList<SectionQuarterCell> Grid);

    // ---------- Entries ----------

    [HttpGet("entries")]
    [HasPermission(Permissions.Tds.Read)]
    public async Task<ActionResult<IReadOnlyList<TdsEntryDto>>> ListEntries(Guid clientId,
        [FromQuery] string? fy, [FromQuery] TdsQuarter? quarter, [FromQuery] string? section,
        CancellationToken ct)
    {
        var q = _db.TdsEntries.AsNoTracking().Where(e => e.ClientId == clientId);
        if (!string.IsNullOrWhiteSpace(fy)) q = q.Where(e => e.FinancialYear == fy);
        if (quarter is not null) q = q.Where(e => e.Quarter == quarter);
        if (!string.IsNullOrWhiteSpace(section)) q = q.Where(e => e.Section == section.ToUpperInvariant());

        var rows = await (from e in q
                          join ch in _db.TdsChallans.AsNoTracking() on e.ChallanId equals ch.Id into chj
                          from ch in chj.DefaultIfEmpty()
                          orderby e.DeductionDate descending
                          select new { e, chNumber = ch == null ? null : ch.ChallanNumber }).ToListAsync(ct);
        return rows.Select(r => new TdsEntryDto(
            r.e.Id, r.e.ClientId, r.e.DeductionDate, r.e.Section, r.e.DeducteeName, r.e.DeducteePan,
            r.e.GrossAmountInr, r.e.TdsRatePercent, r.e.TdsAmountInr,
            r.e.Quarter, r.e.FinancialYear,
            r.e.ChallanId, r.chNumber, r.e.Notes, r.e.Version)).ToList();
    }

    [HttpPost("entries")]
    [HasPermission(Permissions.Tds.Write)]
    public async Task<ActionResult<TdsEntryDto>> CreateEntry(Guid clientId,
        [FromBody] TdsEntryUpsertRequest req, CancellationToken ct)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return UnprocessableEntity(new { error = "unknown_client" });

        var entry = new TdsEntry(clientId, req.DeductionDate, req.Section, req.DeducteeName,
            req.DeducteePan, req.GrossAmountInr, req.TdsRatePercent, req.Notes);
        _db.TdsEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(ListEntries), new { clientId, version = "1.0" }, ToDto(entry, null));
    }

    [HttpPut("entries/{entryId:guid}")]
    [HasPermission(Permissions.Tds.Write)]
    public async Task<ActionResult<TdsEntryDto>> UpdateEntry(Guid clientId, Guid entryId,
        [FromBody] TdsEntryUpsertRequest req, CancellationToken ct)
    {
        var entry = await _db.TdsEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.ClientId == clientId, ct);
        if (entry is null) return NotFound();
        entry.UpdateProfile(req.DeductionDate, req.Section, req.DeducteeName, req.DeducteePan,
            req.GrossAmountInr, req.TdsRatePercent, req.Notes);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry, await ResolveChallanNumberAsync(entry.ChallanId, ct));
    }

    [HttpDelete("entries/{entryId:guid}")]
    [HasPermission(Permissions.Tds.Write)]
    public async Task<IActionResult> DeleteEntry(Guid clientId, Guid entryId, CancellationToken ct)
    {
        var entry = await _db.TdsEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.ClientId == clientId, ct);
        if (entry is null) return NotFound();
        _db.TdsEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------- Challans ----------

    [HttpGet("challans")]
    [HasPermission(Permissions.Tds.Read)]
    public async Task<ActionResult<IReadOnlyList<TdsChallanDto>>> ListChallans(Guid clientId, CancellationToken ct)
    {
        return await _db.TdsChallans.AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.DepositDate)
            .Select(c => new TdsChallanDto(c.Id, c.ClientId, c.ChallanNumber, c.DepositDate,
                c.BankBsrCode, c.Section, c.TotalAmountInr, c.Notes, c.Version))
            .ToListAsync(ct);
    }

    [HttpPost("challans")]
    [HasPermission(Permissions.Tds.Write)]
    public async Task<ActionResult<TdsChallanDto>> CreateChallan(Guid clientId,
        [FromBody] TdsChallanUpsertRequest req, CancellationToken ct)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return UnprocessableEntity(new { error = "unknown_client" });
        if (await _db.TdsChallans.AnyAsync(c => c.ClientId == clientId && c.ChallanNumber == req.ChallanNumber, ct))
            return Conflict(new { error = "duplicate_challan_number" });

        var ch = new TdsChallan(clientId, req.ChallanNumber, req.DepositDate,
            req.BankBsrCode, req.Section, req.TotalAmountInr, req.Notes);
        _db.TdsChallans.Add(ch);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(ListChallans), new { clientId, version = "1.0" },
            new TdsChallanDto(ch.Id, ch.ClientId, ch.ChallanNumber, ch.DepositDate,
                ch.BankBsrCode, ch.Section, ch.TotalAmountInr, ch.Notes, ch.Version));
    }

    [HttpPost("challans/{challanId:guid}/link")]
    [HasPermission(Permissions.Tds.Write)]
    public async Task<IActionResult> LinkEntries(Guid clientId, Guid challanId,
        [FromBody] LinkRequest req, CancellationToken ct)
    {
        var challan = await _db.TdsChallans.FirstOrDefaultAsync(c => c.Id == challanId && c.ClientId == clientId, ct);
        if (challan is null) return NotFound();

        var entries = await _db.TdsEntries
            .Where(e => e.ClientId == clientId && req.EntryIds.Contains(e.Id))
            .ToListAsync(ct);
        foreach (var e in entries) e.LinkToChallan(challanId);
        await _db.SaveChangesAsync(ct);
        return Ok(new { linked = entries.Count });
    }

    [HttpPost("entries/{entryId:guid}/unlink")]
    [HasPermission(Permissions.Tds.Write)]
    public async Task<IActionResult> UnlinkEntry(Guid clientId, Guid entryId, CancellationToken ct)
    {
        var entry = await _db.TdsEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.ClientId == clientId, ct);
        if (entry is null) return NotFound();
        entry.LinkToChallan(null);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------- Summary ----------

    [HttpGet("summary")]
    [HasPermission(Permissions.Tds.Read)]
    public async Task<ActionResult<Summary>> GetSummary(Guid clientId,
        [FromQuery] string? fy, CancellationToken ct)
    {
        var financialYear = string.IsNullOrWhiteSpace(fy)
            ? TdsQuarterExtensions.FinancialYearFor(DateOnly.FromDateTime(DateTime.UtcNow))
            : fy.Trim();

        var rows = await _db.TdsEntries.AsNoTracking()
            .Where(e => e.ClientId == clientId && e.FinancialYear == financialYear)
            .Select(e => new { e.Section, e.Quarter, e.GrossAmountInr, e.TdsAmountInr, e.ChallanId })
            .ToListAsync(ct);

        var grid = rows
            .GroupBy(r => new { r.Section, r.Quarter })
            .Select(g => new SectionQuarterCell(
                g.Key.Section, g.Key.Quarter, g.Count(),
                Math.Round(g.Sum(x => x.GrossAmountInr), 2),
                Math.Round(g.Sum(x => x.TdsAmountInr), 2),
                Math.Round(g.Where(x => x.ChallanId != null).Sum(x => x.TdsAmountInr), 2)))
            .OrderBy(c => c.Section).ThenBy(c => c.Quarter)
            .ToList();

        return new Summary(financialYear,
            Math.Round(rows.Sum(r => r.GrossAmountInr), 2),
            Math.Round(rows.Sum(r => r.TdsAmountInr), 2),
            Math.Round(rows.Where(r => r.ChallanId != null).Sum(r => r.TdsAmountInr), 2),
            grid);
    }

    // ---------- helpers ----------

    private static TdsEntryDto ToDto(TdsEntry e, string? challanNumber) =>
        new(e.Id, e.ClientId, e.DeductionDate, e.Section, e.DeducteeName, e.DeducteePan,
            e.GrossAmountInr, e.TdsRatePercent, e.TdsAmountInr,
            e.Quarter, e.FinancialYear,
            e.ChallanId, challanNumber, e.Notes, e.Version);

    private async Task<string?> ResolveChallanNumberAsync(Guid? challanId, CancellationToken ct)
    {
        if (challanId is null) return null;
        return await _db.TdsChallans.AsNoTracking()
            .Where(c => c.Id == challanId).Select(c => c.ChallanNumber).FirstOrDefaultAsync(ct);
    }
}
