namespace CMS.Application.Email;

/// <summary>Attachment payload passed to the sender; the stream is read once and NOT disposed by the caller.</summary>
public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public record EmailMessage(
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    string BodyPlainText,
    IReadOnlyList<EmailAttachment> Attachments);

public interface IEmailSender
{
    /// <summary>Deliver the message or throw. The caller is responsible for persisting the outbox row.</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
