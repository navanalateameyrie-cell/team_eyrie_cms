using System.ComponentModel.DataAnnotations;
using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// A tax invoice issued to a client. Immutable once <see cref="InvoiceStatus.Issued"/> except
/// for payments and status transitions driven by them. Numbering is FY-scoped and permanent.
/// </summary>
[Auditable]
public class Invoice : Entity
{
    public string Number { get; private set; } = string.Empty;
    public string FinancialYear { get; private set; } = string.Empty; // "YYYY-YY"

    public Guid ClientId { get; private set; }
    public Guid BranchId { get; private set; }                    // our billing branch
    public Guid PlaceOfSupplyStateId { get; private set; }        // customer's state → determines CGST+SGST vs IGST

    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    public DateOnly IssueDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string? Notes { get; private set; }

    public decimal SubtotalInr { get; private set; }
    public decimal CgstInr { get; private set; }
    public decimal SgstInr { get; private set; }
    public decimal IgstInr { get; private set; }
    public decimal TotalInr { get; private set; }
    public decimal PaidInr { get; private set; }
    public decimal CreditedInr { get; private set; }

    public bool IsIntraState { get; private set; }

    // ---------- E-invoicing (IRN / QR) ----------
    public EInvoiceStatus EInvoiceStatus { get; private set; } = EInvoiceStatus.NotGenerated;

    /// <summary>64-char Invoice Reference Number issued by IRP (NIC). Permanent once generated.</summary>
    public string? Irn { get; private set; }
    public string? AckNo { get; private set; }
    public DateTimeOffset? AckDateUtc { get; private set; }

    /// <summary>Raw QR payload returned by IRP; the PDF renderer encodes this to a QR image.</summary>
    public string? QrText { get; private set; }

    public DateTimeOffset? EInvoiceCancelledAtUtc { get; private set; }
    public string? EInvoiceCancelReason { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public Client? Client { get; private set; }
    public Branch? Branch { get; private set; }

    private readonly List<InvoiceLineItem> _lineItems = new();
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems;

    private Invoice() { }

    public Invoice(string number, string financialYear, Guid clientId, Guid branchId,
                    Guid placeOfSupplyStateId, DateOnly issueDate, DateOnly dueDate,
                    bool isIntraState, string? notes)
    {
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("Number is required", nameof(number));
        if (string.IsNullOrWhiteSpace(financialYear)) throw new ArgumentException("FY is required", nameof(financialYear));
        if (clientId == Guid.Empty) throw new ArgumentException("ClientId is required", nameof(clientId));
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required", nameof(branchId));
        if (placeOfSupplyStateId == Guid.Empty) throw new ArgumentException("PlaceOfSupplyStateId is required", nameof(placeOfSupplyStateId));
        if (dueDate < issueDate) throw new ArgumentException("DueDate cannot precede IssueDate.");

        Number = number.Trim();
        FinancialYear = financialYear.Trim();
        ClientId = clientId;
        BranchId = branchId;
        PlaceOfSupplyStateId = placeOfSupplyStateId;
        IssueDate = issueDate;
        DueDate = dueDate;
        IsIntraState = isIntraState;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = InvoiceStatus.Draft;
        Version = 1;
    }

    public void UpdateProfile(DateOnly issueDate, DateOnly dueDate, string? notes)
    {
        EnsureEditable();
        if (dueDate < issueDate) throw new ArgumentException("DueDate cannot precede IssueDate.");
        IssueDate = issueDate;
        DueDate = dueDate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Touch();
    }

    public void AddLineItem(InvoiceLineItem line)
    {
        EnsureEditable();
        _lineItems.Add(line);
        Recompute();
    }

    public void ClearLineItems()
    {
        EnsureEditable();
        _lineItems.Clear();
        Recompute();
    }

    public void Issue()
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException($"Cannot issue a {Status} invoice.");
        if (_lineItems.Count == 0)
            throw new InvalidOperationException("Cannot issue an invoice with no line items.");
        Status = InvoiceStatus.Issued;
        Touch();
    }

    public void MarkEInvoiced(string irn, string ackNo, DateTimeOffset ackDateUtc, string qrText)
    {
        if (Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
            throw new InvalidOperationException($"Cannot IRN a {Status} invoice.");
        if (EInvoiceStatus == EInvoiceStatus.Generated)
            throw new InvalidOperationException("Invoice already has an IRN.");
        if (string.IsNullOrWhiteSpace(irn)) throw new ArgumentException("IRN required", nameof(irn));
        if (string.IsNullOrWhiteSpace(qrText)) throw new ArgumentException("QR text required", nameof(qrText));

        Irn = irn.Trim();
        AckNo = string.IsNullOrWhiteSpace(ackNo) ? null : ackNo.Trim();
        AckDateUtc = ackDateUtc;
        QrText = qrText;
        EInvoiceStatus = EInvoiceStatus.Generated;
        EInvoiceCancelledAtUtc = null;
        EInvoiceCancelReason = null;
        Touch();
    }

    public void CancelEInvoice(string reason)
    {
        if (EInvoiceStatus != EInvoiceStatus.Generated)
            throw new InvalidOperationException("No active IRN to cancel.");
        EInvoiceStatus = EInvoiceStatus.Cancelled;
        EInvoiceCancelledAtUtc = DateTimeOffset.UtcNow;
        EInvoiceCancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Touch();
    }

    public void Cancel()
    {
        if (Status == InvoiceStatus.Paid)
            throw new InvalidOperationException("Paid invoices cannot be cancelled.");
        Status = InvoiceStatus.Cancelled;
        Touch();
    }

    /// <summary>Apply a payment amount; recomputes <see cref="PaidInr"/> and transitions status.</summary>
    public void RecordPayment(decimal amountInr)
    {
        if (Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
            throw new InvalidOperationException($"Cannot record payment on a {Status} invoice.");
        if (amountInr <= 0) throw new ArgumentOutOfRangeException(nameof(amountInr));

        PaidInr = Math.Round(PaidInr + amountInr, 2, MidpointRounding.AwayFromZero);
        RefreshStatus();
        Touch();
    }

    /// <summary>Reverse a payment amount (e.g. bounced cheque).</summary>
    public void ReversePayment(decimal amountInr)
    {
        if (amountInr <= 0) throw new ArgumentOutOfRangeException(nameof(amountInr));
        PaidInr = Math.Round(Math.Max(0m, PaidInr - amountInr), 2, MidpointRounding.AwayFromZero);
        RefreshStatus();
        Touch();
    }

    /// <summary>Apply a credit note amount; counts toward settlement without creating a Payment row.</summary>
    public void ApplyCredit(decimal amountInr)
    {
        if (Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
            throw new InvalidOperationException($"Cannot credit a {Status} invoice.");
        if (amountInr <= 0) throw new ArgumentOutOfRangeException(nameof(amountInr));
        if (Math.Round(PaidInr + CreditedInr + amountInr, 2) > TotalInr)
            throw new InvalidOperationException("Credit would exceed invoice total.");
        CreditedInr = Math.Round(CreditedInr + amountInr, 2);
        RefreshStatus();
        Touch();
    }

    /// <summary>Reverse a previously applied credit (e.g. when a credit note is cancelled).</summary>
    public void ReverseCredit(decimal amountInr)
    {
        if (amountInr <= 0) throw new ArgumentOutOfRangeException(nameof(amountInr));
        CreditedInr = Math.Round(Math.Max(0m, CreditedInr - amountInr), 2);
        RefreshStatus();
        Touch();
    }

    private void RefreshStatus()
    {
        if (Status == InvoiceStatus.Cancelled) return;
        var covered = Math.Round(PaidInr + CreditedInr, 2);
        Status = covered <= 0 ? InvoiceStatus.Issued
               : covered >= TotalInr ? InvoiceStatus.Paid
               : InvoiceStatus.PartPaid;
    }

    /// <summary>Recompute invoice totals + GST split from the current line items.</summary>
    public void Recompute()
    {
        SubtotalInr = Math.Round(_lineItems.Sum(l => l.LineSubtotalInr), 2, MidpointRounding.AwayFromZero);
        var totalGst = Math.Round(_lineItems.Sum(l => l.LineGstInr), 2, MidpointRounding.AwayFromZero);
        if (IsIntraState)
        {
            // Split evenly, resolving the half-paisa by giving the extra to CGST.
            SgstInr = Math.Round(totalGst / 2m, 2, MidpointRounding.AwayFromZero);
            CgstInr = Math.Round(totalGst - SgstInr, 2, MidpointRounding.AwayFromZero);
            IgstInr = 0m;
        }
        else
        {
            IgstInr = totalGst;
            CgstInr = 0m;
            SgstInr = 0m;
        }
        TotalInr = Math.Round(SubtotalInr + CgstInr + SgstInr + IgstInr, 2, MidpointRounding.AwayFromZero);
    }

    private void EnsureEditable()
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException($"A {Status} invoice cannot be edited.");
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
