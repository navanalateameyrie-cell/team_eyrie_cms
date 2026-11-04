using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// One billable row on an invoice. Totals are derived (<c>Quantity × UnitPriceInr</c> → LineSubtotal
/// → GST applied) and stored to lock the amount at the moment the invoice is finalised.
/// </summary>
[Auditable]
public class InvoiceLineItem : Entity
{
    public Guid InvoiceId { get; private set; }
    public int LineNumber { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public Guid? ServiceDefinitionId { get; private set; }
    public Guid? ServiceFileId { get; private set; }
    public Guid? ComplianceObligationId { get; private set; }

    public decimal Quantity { get; private set; }
    public decimal UnitPriceInr { get; private set; }
    public decimal GstRatePercent { get; private set; }

    public decimal LineSubtotalInr { get; private set; }
    public decimal LineGstInr { get; private set; }
    public decimal LineTotalInr { get; private set; }

    private InvoiceLineItem() { }

    public InvoiceLineItem(Guid invoiceId, int lineNumber, string description,
                            Guid? serviceDefinitionId, Guid? serviceFileId, Guid? complianceObligationId,
                            decimal quantity, decimal unitPriceInr, decimal gstRatePercent)
    {
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId is required", nameof(invoiceId));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required", nameof(description));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPriceInr < 0) throw new ArgumentOutOfRangeException(nameof(unitPriceInr));
        if (gstRatePercent < 0 || gstRatePercent > 100) throw new ArgumentOutOfRangeException(nameof(gstRatePercent));

        InvoiceId = invoiceId;
        LineNumber = lineNumber;
        Description = description.Trim();
        ServiceDefinitionId = serviceDefinitionId == Guid.Empty ? null : serviceDefinitionId;
        ServiceFileId = serviceFileId == Guid.Empty ? null : serviceFileId;
        ComplianceObligationId = complianceObligationId == Guid.Empty ? null : complianceObligationId;
        Quantity = Math.Round(quantity, 3, MidpointRounding.AwayFromZero);
        UnitPriceInr = Math.Round(unitPriceInr, 2, MidpointRounding.AwayFromZero);
        GstRatePercent = Math.Round(gstRatePercent, 2, MidpointRounding.AwayFromZero);
        Recompute();
    }

    private void Recompute()
    {
        LineSubtotalInr = Math.Round(Quantity * UnitPriceInr, 2, MidpointRounding.AwayFromZero);
        LineGstInr = Math.Round(LineSubtotalInr * GstRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
        LineTotalInr = LineSubtotalInr + LineGstInr;
    }
}
