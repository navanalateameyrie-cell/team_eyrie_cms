namespace CMS.Infrastructure.Email;

/// <summary>
/// SMTP + sender identity. When <see cref="SmtpHost"/> is empty the DI pipeline falls back to a
/// filesystem sender that writes <c>.eml</c> files under <see cref="DevDropFolder"/> — useful for
/// local development and in integration tests where no real relay is reachable.
/// </summary>
public class MailOptions
{
    public const string SectionName = "Mail";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }

    public string FromAddress { get; set; } = "noreply@cms.local";
    public string FromName { get; set; } = "CMS";

    /// <summary>Fallback drop folder when SMTP is not configured; absolute or ContentRoot-relative.</summary>
    public string? DevDropFolder { get; set; }
}
