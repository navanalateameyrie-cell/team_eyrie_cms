namespace CMS.Application.Billing;

/// <summary>Shape returned by an IRN-generation call. Keep it provider-agnostic.</summary>
public record EInvoiceResult(string Irn, string AckNo, DateTimeOffset AckDateUtc, string QrText);

public interface IEInvoiceProvider
{
    /// <summary>Submit an invoice to the IRP and get back IRN + QR payload. Throws on provider error.</summary>
    Task<EInvoiceResult> GenerateAsync(InvoicePdfModel invoice, CancellationToken ct);

    /// <summary>Cancel a previously issued IRN with a reason. Returns true on IRP ack.</summary>
    Task<bool> CancelAsync(string irn, string reason, CancellationToken ct);
}
