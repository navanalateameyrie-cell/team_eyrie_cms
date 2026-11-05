using Asp.Versioning;
using CMS.Api.Authorization;
using CMS.Application.Authorization;
using CMS.Domain.Entities;
using CMS.Infrastructure.Billing;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMS.Api.Controllers;

/// <summary>Aggregated views over Clients, Billing and Compliance for the dashboards and reports page.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
public class ReportsController : ControllerBase
{
    private readonly CmsDbContext _db;
    public ReportsController(CmsDbContext db) => _db = db;

    // --------- Revenue monthly (12 buckets of a FY, optional service slice) ---------

    public record RevenueMonthlyPoint(int Year, int Month, string Label, decimal AmountInr, int InvoiceCount);

    [HttpGet("revenue-monthly")]
    [HasPermission(Permissions.Reports.Read)]
    public async Task<ActionResult<IReadOnlyList<RevenueMonthlyPoint>>> RevenueMonthly(
        [FromQuery] string? fy, [FromQuery] string? serviceCode, CancellationToken ct)
    {
        var financialYear = string.IsNullOrWhiteSpace(fy)
            ? InvoiceNumberGenerator.FinancialYearFor(DateOnly.FromDateTime(DateTime.UtcNow))
            : fy.Trim();

        var baseQuery = _db.Invoices.AsNoTracking()
            .Where(i => i.FinancialYear == financialYear
                        && i.Status != InvoiceStatus.Draft
                        && i.Status != InvoiceStatus.Cancelled);

        List<(DateOnly IssueDate, decimal TotalInr, Guid Id)> rows;
        if (!string.IsNullOrWhiteSpace(serviceCode))
        {
            var code = serviceCode.Trim().ToUpperInvariant();
            // Any line item on this invoice refers to a service with this code.
            var raw = await (from i in baseQuery
                             join l in _db.InvoiceLineItems.AsNoTracking() on i.Id equals l.InvoiceId
                             join s in _db.ServiceDefinitions.AsNoTracking() on l.ServiceDefinitionId equals s.Id
                             where s.Code == code
                             select new { i.Id, i.IssueDate, i.TotalInr }).Distinct().ToListAsync(ct);
            rows = raw.Select(r => (r.IssueDate, r.TotalInr, r.Id)).ToList();
        }
        else
        {
            rows = await baseQuery
                .Select(i => new { i.IssueDate, i.TotalInr, i.Id })
                .ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(r => (r.IssueDate, r.TotalInr, r.Id)).ToList(), ct);
        }

        // Build 12 Apr→Mar buckets even when there are zero rows for a month.
        var fyStartYear = int.Parse(financialYear.Split('-')[0]);
        var buckets = Enumerable.Range(0, 12).Select(offset =>
        {
            var month = ((3 + offset) % 12) + 1;          // Apr → Mar
            var year = fyStartYear + ((offset + 3) / 12); // Apr-Dec = fyStart; Jan-Mar = fyStart+1
            var monthRows = rows.Where(r => r.IssueDate.Year == year && r.IssueDate.Month == month).ToList();
            var label = new DateOnly(year, month, 1).ToString("MMM yy");
            return new RevenueMonthlyPoint(year, month, label,
                Math.Round(monthRows.Sum(r => r.TotalInr), 2),
                monthRows.Count);
        }).ToList();
        return buckets;
    }

    // --------- Outstanding ageing ---------

    public record AgeingBucket(string Label, decimal AmountInr, int InvoiceCount);

    [HttpGet("outstanding-ageing")]
    [HasPermission(Permissions.Reports.Read)]
    public async Task<ActionResult<IReadOnlyList<AgeingBucket>>> OutstandingAgeing(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var open = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartPaid)
            .Select(i => new { i.DueDate, i.TotalInr, i.PaidInr })
            .ToListAsync(ct);

        int Days(DateOnly d) => today.DayNumber - d.DayNumber; // positive = overdue
        string LabelFor(int ageDays) => ageDays switch
        {
            <= 0  => "Current",
            <= 30 => "1-30",
            <= 60 => "31-60",
            <= 90 => "61-90",
            _     => "90+",
        };

        var ordered = new[] { "Current", "1-30", "31-60", "61-90", "90+" };
        var grouped = open
            .Select(r => new { Label = LabelFor(Days(r.DueDate)), Due = Math.Max(0m, r.TotalInr - r.PaidInr) })
            .GroupBy(x => x.Label)
            .ToDictionary(g => g.Key, g => (Amount: g.Sum(x => x.Due), Count: g.Count()));
        return ordered
            .Select(l => new AgeingBucket(l,
                Math.Round(grouped.GetValueOrDefault(l).Amount, 2),
                grouped.GetValueOrDefault(l).Count))
            .ToList();
    }

    // --------- Top clients ---------

    public record TopClient(Guid ClientId, string ClientCode, string ClientName, decimal RevenueInr, int InvoiceCount);

    [HttpGet("top-clients")]
    [HasPermission(Permissions.Reports.Read)]
    public async Task<ActionResult<IReadOnlyList<TopClient>>> TopClientsByRevenue(
        [FromQuery] string? fy, [FromQuery] int take = 10, CancellationToken ct = default)
    {
        if (take <= 0 || take > 100) take = 10;
        var financialYear = string.IsNullOrWhiteSpace(fy)
            ? InvoiceNumberGenerator.FinancialYearFor(DateOnly.FromDateTime(DateTime.UtcNow))
            : fy.Trim();

        var rows = await (from i in _db.Invoices.AsNoTracking()
                          where i.FinancialYear == financialYear
                                && i.Status != InvoiceStatus.Draft
                                && i.Status != InvoiceStatus.Cancelled
                          join c in _db.Clients.AsNoTracking() on i.ClientId equals c.Id
                          select new { c.Id, c.Code, c.LegalName, i.TotalInr }).ToListAsync(ct);
        return rows
            .GroupBy(r => new { r.Id, r.Code, r.LegalName })
            .Select(g => new TopClient(g.Key.Id, g.Key.Code, g.Key.LegalName,
                Math.Round(g.Sum(x => x.TotalInr), 2), g.Count()))
            .OrderByDescending(t => t.RevenueInr)
            .Take(take)
            .ToList();
    }

    // --------- Compliance stats per service ---------

    public record ComplianceServiceStat(string ServiceCode, string ServiceName,
                                         int Pending, int InProgress, int Filed, int Delayed, int Waived, int Overdue);

    [HttpGet("compliance-stats")]
    [HasPermission(Permissions.Reports.Read)]
    public async Task<ActionResult<IReadOnlyList<ComplianceServiceStat>>> ComplianceStats(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await (from o in _db.ComplianceObligations.AsNoTracking()
                          join f in _db.ServiceFiles.AsNoTracking() on o.ServiceFileId equals f.Id
                          join s in _db.ServiceDefinitions.AsNoTracking() on f.ServiceDefinitionId equals s.Id
                          select new { s.Code, s.Name, o.Status, o.DueDate }).ToListAsync(ct);
        return rows
            .GroupBy(r => new { r.Code, r.Name })
            .Select(g => new ComplianceServiceStat(
                g.Key.Code, g.Key.Name,
                Pending:    g.Count(x => x.Status == ComplianceStatus.Pending),
                InProgress: g.Count(x => x.Status == ComplianceStatus.InProgress),
                Filed:      g.Count(x => x.Status == ComplianceStatus.Filed),
                Delayed:    g.Count(x => x.Status == ComplianceStatus.Delayed),
                Waived:     g.Count(x => x.Status == ComplianceStatus.Waived),
                Overdue:    g.Count(x => (x.Status == ComplianceStatus.Pending || x.Status == ComplianceStatus.InProgress)
                                          && x.DueDate < today)))
            .OrderBy(x => x.ServiceCode)
            .ToList();
    }

    // --------- Client growth (last N months of new client creations) ---------

    public record ClientGrowthPoint(int Year, int Month, string Label, int NewClients);

    [HttpGet("client-growth")]
    [HasPermission(Permissions.Reports.Read)]
    public async Task<ActionResult<IReadOnlyList<ClientGrowthPoint>>> ClientGrowth(
        [FromQuery] int months = 12, CancellationToken ct = default)
    {
        if (months <= 0 || months > 60) months = 12;

        // Materialize minimal columns, bucket in memory — SQLite can't group on DateTimeOffset.
        var created = await _db.Clients.AsNoTracking()
            .Select(c => c.CreatedAtUtc).ToListAsync(ct);
        var today = DateTime.UtcNow;
        var startAnchor = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

        return Enumerable.Range(0, months).Select(offset =>
        {
            var monthStart = startAnchor.AddMonths(offset);
            var monthEnd = monthStart.AddMonths(1);
            var count = created.Count(d => d.UtcDateTime >= monthStart && d.UtcDateTime < monthEnd);
            return new ClientGrowthPoint(monthStart.Year, monthStart.Month,
                monthStart.ToString("MMM yy"), count);
        }).ToList();
    }
}
