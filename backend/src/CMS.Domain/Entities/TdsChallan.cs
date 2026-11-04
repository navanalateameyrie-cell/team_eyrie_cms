using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A tax-deposit challan the practice has filed on behalf of a client. One challan typically
/// covers a single section × quarter but can span multiple entries. We keep it loose — only the
/// link from entry → challan enforces membership.
/// </summary>
[Auditable]
public class TdsChallan : Entity
{
    public Guid ClientId { get; private set; }

    /// <summary>Challan serial / CIN. Free-form so the practice can mirror the actual slip.</summary>
    public string ChallanNumber { get; private set; } = string.Empty;

    public DateOnly DepositDate { get; private set; }
    public string? BankBsrCode { get; private set; }

    public string Section { get; private set; } = string.Empty;
    public decimal TotalAmountInr { get; private set; }

    public string? Notes { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public Client? Client { get; private set; }

    private TdsChallan() { }

    public TdsChallan(Guid clientId, string challanNumber, DateOnly depositDate,
                       string? bankBsrCode, string section, decimal totalAmountInr, string? notes)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId required", nameof(clientId));
        if (string.IsNullOrWhiteSpace(challanNumber)) throw new ArgumentException("ChallanNumber required", nameof(challanNumber));
        if (string.IsNullOrWhiteSpace(section)) throw new ArgumentException("Section required", nameof(section));
        if (totalAmountInr < 0) throw new ArgumentOutOfRangeException(nameof(totalAmountInr));

        ClientId = clientId;
        ChallanNumber = challanNumber.Trim();
        DepositDate = depositDate;
        BankBsrCode = string.IsNullOrWhiteSpace(bankBsrCode) ? null : bankBsrCode.Trim();
        Section = section.Trim().ToUpperInvariant();
        TotalAmountInr = Math.Round(totalAmountInr, 2, MidpointRounding.AwayFromZero);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Version = 1;
    }

    public void UpdateProfile(string challanNumber, DateOnly depositDate,
                               string? bankBsrCode, string section, decimal totalAmountInr, string? notes)
    {
        if (string.IsNullOrWhiteSpace(challanNumber)) throw new ArgumentException("ChallanNumber required", nameof(challanNumber));
        if (string.IsNullOrWhiteSpace(section)) throw new ArgumentException("Section required", nameof(section));
        if (totalAmountInr < 0) throw new ArgumentOutOfRangeException(nameof(totalAmountInr));

        ChallanNumber = challanNumber.Trim();
        DepositDate = depositDate;
        BankBsrCode = string.IsNullOrWhiteSpace(bankBsrCode) ? null : bankBsrCode.Trim();
        Section = section.Trim().ToUpperInvariant();
        TotalAmountInr = Math.Round(totalAmountInr, 2, MidpointRounding.AwayFromZero);
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
