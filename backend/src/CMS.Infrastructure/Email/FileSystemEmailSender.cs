using System.Text;
using CMS.Application.Email;
using CMS.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMS.Infrastructure.Email;

/// <summary>
/// Dev fallback: writes each message as a minimal RFC-822 <c>.eml</c> file under the configured
/// drop folder, so you can inspect the exact message that WOULD have gone out without needing a
/// real relay. Mail clients (Outlook, Thunderbird) open these files natively.
/// </summary>
public class FileSystemEmailSender : IEmailSender
{
    private readonly ISettingsStore _settings;
    private readonly MailOptions _bootstrap;
    private readonly ILogger<FileSystemEmailSender> _log;

    public FileSystemEmailSender(ISettingsStore settings, IOptions<MailOptions> bootstrap,
                                   ILogger<FileSystemEmailSender> log)
    {
        _settings = settings;
        _bootstrap = bootstrap.Value;
        _log = log;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var _opts = await _settings.GetAsync<MailOptions>(SettingKeys.Mail, ct);
        var dropFolder = string.IsNullOrWhiteSpace(_opts.DevDropFolder) ? _bootstrap.DevDropFolder : _opts.DevDropFolder;
        if (string.IsNullOrWhiteSpace(dropFolder))
            throw new InvalidOperationException("Mail:DevDropFolder is required when SMTP is not configured.");
        var _dropFolder = Path.GetFullPath(dropFolder);
        Directory.CreateDirectory(_dropFolder);
        var boundary = "cms-email-" + Guid.NewGuid().ToString("N");
        var sb = new StringBuilder();
        sb.Append("From: \"").Append(_opts.FromName).Append("\" <").Append(_opts.FromAddress).Append(">\r\n");
        sb.Append("To: ").Append(string.Join(", ", message.To)).Append("\r\n");
        if (message.Cc.Count > 0) sb.Append("Cc: ").Append(string.Join(", ", message.Cc)).Append("\r\n");
        sb.Append("Subject: ").Append(message.Subject).Append("\r\n");
        sb.Append("Date: ").Append(DateTimeOffset.UtcNow.ToString("r")).Append("\r\n");
        sb.Append("MIME-Version: 1.0\r\n");
        if (message.Attachments.Count == 0)
        {
            sb.Append("Content-Type: text/plain; charset=utf-8\r\n\r\n");
            sb.Append(message.BodyPlainText);
        }
        else
        {
            sb.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append("\"\r\n\r\n");
            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("Content-Type: text/plain; charset=utf-8\r\n\r\n");
            sb.Append(message.BodyPlainText).Append("\r\n");
            foreach (var a in message.Attachments)
            {
                sb.Append("--").Append(boundary).Append("\r\n");
                sb.Append("Content-Type: ").Append(a.ContentType).Append("; name=\"").Append(a.FileName).Append("\"\r\n");
                sb.Append("Content-Transfer-Encoding: base64\r\n");
                sb.Append("Content-Disposition: attachment; filename=\"").Append(a.FileName).Append("\"\r\n\r\n");
                sb.Append(Convert.ToBase64String(a.Content, Base64FormattingOptions.InsertLineBreaks)).Append("\r\n");
            }
            sb.Append("--").Append(boundary).Append("--\r\n");
        }

        var filename = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{SanitiseFileName(message.Subject)}-{Guid.NewGuid().ToString("N")[..6]}.eml";
        var path = Path.Combine(_dropFolder, filename);
        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, ct);
        _log.LogInformation("Email dropped to {Path} (subject: {Subject})", path, message.Subject);
    }

    private static string SanitiseFileName(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(s.Where(ch => !invalid.Contains(ch) && ch != ' ').ToArray());
        return cleaned.Length > 40 ? cleaned[..40] : (cleaned.Length == 0 ? "message" : cleaned);
    }
}
