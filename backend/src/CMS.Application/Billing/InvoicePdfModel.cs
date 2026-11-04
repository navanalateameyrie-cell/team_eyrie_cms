using CMS.Domain.Entities;

namespace CMS.Application.Billing;

/// <summary>Flat data a PDF renderer needs to produce a tax invoice. No EF entities leak across the layer.</summary>
public record InvoicePdfModel(
    string Number, string FinancialYear,
    DateOnly IssueDate, DateOnly DueDate,
    InvoiceStatus Status,
    string ClientName, string? ClientPan, string? ClientGstin, string? ClientAddress,
    string PlaceOfSupplyState,
    bool IsIntraState,
    IReadOnlyList<InvoicePdfLine> Lines,
    decimal SubtotalInr, decimal CgstInr, decimal SgstInr, decimal IgstInr,
    decimal TotalInr, decimal PaidInr, decimal OutstandingInr,
    string? Notes)
{
    /// <summary>Set after the IRP returns an IRN; the renderer embeds these as a QR + ack block.</summary>
    public string? Irn { get; init; }
    public string? AckNo { get; init; }
    public DateTimeOffset? AckDateUtc { get; init; }
    public string? QrText { get; init; }

    /// <summary>Set when the invoice has been digitally signed; renderer stamps a visible footer.</summary>
    public string? SignerName { get; init; }
    public string? SignatureThumbprint { get; init; }
    public DateTimeOffset? SignedAtUtc { get; init; }
}

public record InvoicePdfLine(
    int LineNumber,
    string Description,
    string? ServiceCode,
    string? FileNumber,
    string? ObligationPeriodKey,
    decimal Quantity,
    decimal UnitPriceInr,
    decimal GstRatePercent,
    decimal LineSubtotalInr,
    decimal LineGstInr,
    decimal LineTotalInr);
