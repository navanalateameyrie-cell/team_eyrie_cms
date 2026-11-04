using CMS.Application.Abstractions;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CMS.Infrastructure.Billing;

/// <summary>
/// Produces monotonically increasing FY-scoped invoice numbers (e.g. <c>INV/2024-25/0001</c>).
/// Serialises writes via an atomic UPDATE on <see cref="InvoiceNumberCounter"/>, mirroring the
/// client code generator pattern.
/// </summary>
public class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private const long StartingValue = 1;

    private readonly CmsDbContext _db;
    public InvoiceNumberGenerator(CmsDbContext db) => _db = db;

    public async Task<string> NextAsync(string financialYear, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(financialYear)) throw new ArgumentException("financialYear is required", nameof(financialYear));
        financialYear = financialYear.Trim();

        var ambient = _db.Database.CurrentTransaction;
        var tx = ambient is null ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var affected = await _db.InvoiceNumberCounters
                .Where(c => c.FinancialYear == financialYear)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.NextValue, c => c.NextValue + 1), ct);

            long assigned;
            if (affected == 0)
            {
                _db.InvoiceNumberCounters.Add(new InvoiceNumberCounter(financialYear, StartingValue + 1));
                await _db.SaveChangesAsync(ct);
                assigned = StartingValue;
            }
            else
            {
                var updated = await _db.InvoiceNumberCounters.AsNoTracking()
                    .Where(c => c.FinancialYear == financialYear)
                    .Select(c => c.NextValue).FirstAsync(ct);
                assigned = updated - 1;
            }

            if (tx is not null) await tx.CommitAsync(ct);
            return $"INV/{financialYear}/{assigned:D4}";
        }
        finally { if (tx is not null) await tx.DisposeAsync(); }
    }

    public async Task<string> NextForKindAsync(string kind, string financialYear, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("kind is required", nameof(kind));
        if (string.IsNullOrWhiteSpace(financialYear)) throw new ArgumentException("financialYear is required", nameof(financialYear));
        kind = kind.Trim().ToUpperInvariant();
        financialYear = financialYear.Trim();

        var ambient = _db.Database.CurrentTransaction;
        var tx = ambient is null ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var affected = await _db.DocumentNumberCounters
                .Where(c => c.Kind == kind && c.FinancialYear == financialYear)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.NextValue, c => c.NextValue + 1), ct);

            long assigned;
            if (affected == 0)
            {
                _db.DocumentNumberCounters.Add(new DocumentNumberCounter(kind, financialYear, StartingValue + 1));
                await _db.SaveChangesAsync(ct);
                assigned = StartingValue;
            }
            else
            {
                var updated = await _db.DocumentNumberCounters.AsNoTracking()
                    .Where(c => c.Kind == kind && c.FinancialYear == financialYear)
                    .Select(c => c.NextValue).FirstAsync(ct);
                assigned = updated - 1;
            }

            if (tx is not null) await tx.CommitAsync(ct);
            return $"{kind}/{financialYear}/{assigned:D4}";
        }
        finally { if (tx is not null) await tx.DisposeAsync(); }
    }

    /// <summary>Return the Indian FY (<c>YYYY-YY</c>) that contains the given date (Apr-start).</summary>
    public static string FinancialYearFor(DateOnly d)
    {
        var startYear = d.Month >= 4 ? d.Year : d.Year - 1;
        var endYear = (startYear + 1) % 100;
        return $"{startYear:0000}-{endYear:00}";
    }
}
