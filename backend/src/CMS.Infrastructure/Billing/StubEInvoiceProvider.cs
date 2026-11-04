using System.Security.Cryptography;
using System.Text;
using CMS.Application.Billing;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Billing;

/// <summary>
/// Dev + test provider. Generates a deterministic 64-char IRN from a SHA-256 of the invoice number
/// plus the current date, so repeated calls on the same invoice-day produce the same artefact.
/// The QR payload mirrors the IRP's "canonical string" shape so the PDF renderer treats it the
/// same way it would a real payload.
/// </summary>
public class StubEInvoiceProvider : IEInvoiceProvider
{
    private readonly ILogger<StubEInvoiceProvider> _log;
    public StubEInvoiceProvider(ILogger<StubEInvoiceProvider> log) => _log = log;

    public Task<EInvoiceResult> GenerateAsync(InvoicePdfModel invoice, CancellationToken ct)
    {
        var seed = $"{invoice.Number}|{invoice.IssueDate:yyyyMMdd}|{invoice.TotalInr}";
        using var sha = SHA256.Create();
        var irn = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant();
        var ackNo = (BitConverter.ToUInt64(sha.ComputeHash(Encoding.UTF8.GetBytes(seed + "|ack")), 0) % 100_000_000_000UL).ToString();
        var ackDate = DateTimeOffset.UtcNow;
        var qrPayload =
            $"irn:{irn};ackno:{ackNo};ackdt:{ackDate:yyyyMMddHHmmss};inv:{invoice.Number};" +
            $"doc_dt:{invoice.IssueDate:yyyyMMdd};total:{invoice.TotalInr:F2};pos:{invoice.PlaceOfSupplyState}";
        _log.LogInformation("StubEInvoice: generated IRN {Irn} for {Invoice}", irn[..12], invoice.Number);
        return Task.FromResult(new EInvoiceResult(irn, ackNo, ackDate, qrPayload));
    }

    public Task<bool> CancelAsync(string irn, string reason, CancellationToken ct)
    {
        _log.LogInformation("StubEInvoice: cancel ack for IRN {Irn} reason={Reason}", irn[..Math.Min(12, irn.Length)], reason);
        return Task.FromResult(true);
    }
}
