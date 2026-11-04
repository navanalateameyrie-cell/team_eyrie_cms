using System.Net;
using System.Net.Mail;
using CMS.Application.Email;
using CMS.Application.Settings;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Email;

/// <summary>Standard SMTP sender built on <see cref="SmtpClient"/>. Picks up credentials from <see cref="MailOptions"/>.</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly ISettingsStore _settings;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(ISettingsStore settings, ILogger<SmtpEmailSender> log)
    {
        _settings = settings;
        _log = log;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var _opts = await _settings.GetAsync<MailOptions>(SettingKeys.Mail, ct);
        if (string.IsNullOrWhiteSpace(_opts.SmtpHost))
            throw new InvalidOperationException("SMTP host is not configured.");

        using var msg = new MailMessage
        {
            From = new MailAddress(_opts.FromAddress, _opts.FromName),
            Subject = message.Subject,
            Body = message.BodyPlainText,
            IsBodyHtml = false,
        };
        foreach (var t in message.To) msg.To.Add(new MailAddress(t));
        foreach (var c in message.Cc) msg.CC.Add(new MailAddress(c));
        foreach (var a in message.Attachments)
        {
            var stream = new MemoryStream(a.Content);
            msg.Attachments.Add(new Attachment(stream, a.FileName, a.ContentType));
        }

        using var client = new SmtpClient(_opts.SmtpHost, _opts.SmtpPort)
        {
            EnableSsl = _opts.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = string.IsNullOrWhiteSpace(_opts.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_opts.Username, _opts.Password),
        };

        _log.LogInformation("SMTP send: {Subject} → {To} ({Count} attachments)",
            message.Subject, string.Join(";", message.To), message.Attachments.Count);
        await client.SendMailAsync(msg, ct);
    }
}
