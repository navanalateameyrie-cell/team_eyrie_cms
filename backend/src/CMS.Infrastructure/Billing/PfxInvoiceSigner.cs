using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using CMS.Application.Billing;
using CMS.Application.Settings;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Billing;

/// <summary>
/// Produces a detached PKCS#7 (CMS) signature over the raw PDF bytes using a .pfx held on disk.
/// Verifiers can validate the <c>.p7s</c> against the PDF with any CMS-aware tool (openssl, Adobe,
/// Windows crypto APIs). We avoid inline PAdES to keep clear of iText7's AGPL license.
/// </summary>
public class PfxInvoiceSigner : IInvoiceSigner
{
    private readonly ISettingsStore _settings;
    private readonly ILogger<PfxInvoiceSigner> _log;

    public PfxInvoiceSigner(ISettingsStore settings, ILogger<PfxInvoiceSigner> log)
    {
        _settings = settings;
        _log = log;
    }

    public async Task<InvoiceSignature?> SignAsync(byte[] pdfBytes, CancellationToken ct = default)
    {
        var opts = await _settings.GetAsync<DigitalSignatureOptions>(SettingKeys.DigitalSignature, ct);
        if (!opts.Enabled || string.IsNullOrWhiteSpace(opts.CertificatePath))
            return null;
        if (!File.Exists(opts.CertificatePath))
        {
            _log.LogWarning("DigitalSignature.CertificatePath {Path} does not exist; skipping sign.", opts.CertificatePath);
            return null;
        }

        X509Certificate2 cert;
        try
        {
            cert = new X509Certificate2(opts.CertificatePath, opts.CertificatePassword ?? string.Empty,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "DigitalSignature: failed to load .pfx; skipping sign.");
            return null;
        }

        if (!cert.HasPrivateKey)
        {
            _log.LogWarning("DigitalSignature: certificate has no private key; skipping sign.");
            cert.Dispose();
            return null;
        }

        try
        {
            var content = new ContentInfo(pdfBytes);
            var cms = new SignedCms(content, detached: true);
            // CmsSigner(cert) defaults to IssuerAndSerialNumber + SHA-256; keeps us off the
            // vulnerable System.Security.Cryptography.Pkcs NuGet (bits ship with the SDK anyway).
            cms.ComputeSignature(new CmsSigner(cert)
            {
                DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"), // SHA-256
                IncludeOption = X509IncludeOption.WholeChain,
            }, silent: true);
            var p7s = cms.Encode();

            var signerName = string.IsNullOrWhiteSpace(opts.SignerName) ? cert.GetNameInfo(X509NameType.SimpleName, false) : opts.SignerName!;
            return new InvoiceSignature(
                SignerName: signerName,
                CertificateThumbprint: cert.Thumbprint,
                SignedAtUtc: DateTimeOffset.UtcNow,
                DetachedCms: p7s);
        }
        finally { cert.Dispose(); }
    }
}

/// <summary>Default no-op signer. DI selects this when signing is disabled or unconfigured.</summary>
public class NullInvoiceSigner : IInvoiceSigner
{
    public Task<InvoiceSignature?> SignAsync(byte[] pdfBytes, CancellationToken ct = default) =>
        Task.FromResult<InvoiceSignature?>(null);
}
