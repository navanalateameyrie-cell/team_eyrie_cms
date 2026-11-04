namespace CMS.Infrastructure.Billing;

/// <summary>
/// Digital-signature configuration for invoice PDFs. When <see cref="Enabled"/> is true AND a
/// usable certificate loads from <see cref="CertificatePath"/>, the signer runs and the renderer
/// stamps a visible signature footer; the detached <c>.p7s</c> is served separately.
/// </summary>
public class DigitalSignatureOptions
{
    public const string SectionName = "DigitalSignature";

    public bool Enabled { get; set; }

    /// <summary>Absolute filesystem path to the .pfx holding the signing key + certificate.</summary>
    public string? CertificatePath { get; set; }

    public string? CertificatePassword { get; set; }

    /// <summary>Human-readable name shown on the PDF stamp (e.g. "Nava Nala Associates").</summary>
    public string? SignerName { get; set; }
}
