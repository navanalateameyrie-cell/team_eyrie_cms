using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// One deduction entry — a vendor payment the client has withheld tax from. The computed TDS
/// amount and the derived quarter/FY are frozen at construction so later edits can replay the
/// same math and reporting is stable.
/// </summary>
[Auditable]
public class TdsEntry : Entity
{
    public Guid ClientId { get; private set; }
    public DateOnly DeductionDate { get; private set; }

    /// <summary>Short section code — 194C / 194J / 194I / 192 / 194A etc.</summary>
    public string Section { get; private set; } = string.Empty;

    public string DeducteeName { get; private set; } = string.Empty;
    public string? DeducteePan { get; private set; }

    public decimal GrossAmountInr { get; private set; }
    public decimal TdsRatePercent { get; private set; }
    public decimal TdsAmountInr { get; private set; }

    public TdsQuarter Quarter { get; private set; }
    public string FinancialYear { get; private set; } = string.Empty;

    public Guid? ChallanId { get; private set; }
    public string? Notes { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public Client? Client { get; private set; }
    public TdsChallan? Challan { get; private set; }

    private TdsEntry() { }

    public TdsEntry(Guid clientId, DateOnly deductionDate, string section,
                     string deducteeName, string? deducteePan,
                     decimal grossAmountInr, decimal tdsRatePercent, string? notes)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId required", nameof(clientId));
        if (string.IsNullOrWhiteSpace(section)) throw new ArgumentException("Section required", nameof(section));
        if (string.IsNullOrWhiteSpace(deducteeName)) throw new ArgumentException("DeducteeName required", nameof(deducteeName));
        if (grossAmountInr < 0) throw new ArgumentOutOfRangeException(nameof(grossAmountInr));
        if (tdsRatePercent < 0 || tdsRatePercent > 100) throw new ArgumentOutOfRangeException(nameof(tdsRatePercent));

        ClientId = clientId;
        DeductionDate = deductionDate;
        Section = section.Trim().ToUpperInvariant();
        DeducteeName = deducteeName.Trim();
        DeducteePan = string.IsNullOrWhiteSpace(deducteePan) ? null : deducteePan.Trim().ToUpperInvariant();
        GrossAmountInr = Math.Round(grossAmountInr, 2, MidpointRounding.AwayFromZero);
        TdsRatePercent = Math.Round(tdsRatePercent, 2, MidpointRounding.AwayFromZero);
        TdsAmountInr = Math.Round(GrossAmountInr * TdsRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
        Quarter = TdsQuarterExtensions.QuarterFor(deductionDate);
        FinancialYear = TdsQuarterExtensions.FinancialYearFor(deductionDate);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Version = 1;
    }

    public void UpdateProfile(DateOnly deductionDate, string section,
                               string deducteeName, string? deducteePan,
                               decimal grossAmountInr, decimal tdsRatePercent, string? notes)
    {
        if (string.IsNullOrWhiteSpace(section)) throw new ArgumentException("Section required", nameof(section));
        if (string.IsNullOrWhiteSpace(deducteeName)) throw new ArgumentException("DeducteeName required", nameof(deducteeName));
        if (grossAmountInr < 0) throw new ArgumentOutOfRangeException(nameof(grossAmountInr));
        if (tdsRatePercent < 0 || tdsRatePercent > 100) throw new ArgumentOutOfRangeException(nameof(tdsRatePercent));

        DeductionDate = deductionDate;
        Section = section.Trim().ToUpperInvariant();
        DeducteeName = deducteeName.Trim();
        DeducteePan = string.IsNullOrWhiteSpace(deducteePan) ? null : deducteePan.Trim().ToUpperInvariant();
        GrossAmountInr = Math.Round(grossAmountInr, 2, MidpointRounding.AwayFromZero);
        TdsRatePercent = Math.Round(tdsRatePercent, 2, MidpointRounding.AwayFromZero);
        TdsAmountInr = Math.Round(GrossAmountInr * TdsRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
        Quarter = TdsQuarterExtensions.QuarterFor(deductionDate);
        FinancialYear = TdsQuarterExtensions.FinancialYearFor(deductionDate);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Touch();
    }

    public void LinkToChallan(Guid? challanId)
    {
        ChallanId = challanId == Guid.Empty ? null : challanId;
        Touch();
    }

    private void Touch() { UpdatedAtUtc = DateTimeOffset.UtcNow; Version++; }
}
