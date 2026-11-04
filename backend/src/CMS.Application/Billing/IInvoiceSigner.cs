namespace CMS.Application.Billing;

/// <summary>Shape returned by a signer; <c>DetachedCms</c> may be null when signing is disabled.</summary>
public record InvoiceSignature(string SignerName, string CertificateThumbprint,
                                 DateTimeOffset SignedAtUtc, byte[]? DetachedCms);

public interface IInvoiceSigner
{
    /// <summary>Compute a detached CMS signature over the given PDF bytes. Returns null when disabled.</summary>
    Task<InvoiceSignature?> SignAsync(byte[] pdfBytes, CancellationToken ct = default);
}
