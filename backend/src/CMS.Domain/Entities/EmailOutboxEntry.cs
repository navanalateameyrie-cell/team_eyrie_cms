using CMS.Domain.Auditing;
using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// Append-only log of every outbound email attempt (success and failure). The sender writes exactly
/// one row per attempt and never updates the business fields — <see cref="MarkSent"/> and
/// <see cref="MarkFailed"/> only move the status and the error message, giving operators a reliable
/// audit trail even when the SMTP relay is unreachable.
/// </summary>
[Auditable]
public class EmailOutboxEntry : Entity
{
    public string ToAddresses { get; private set; } = string.Empty; // comma-joined
    public string? CcAddresses { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string BodyPlainText { get; private set; } = string.Empty;

    public string FromAddress { get; private set; } = string.Empty;
    public string? FromName { get; private set; }

    public int AttachmentCount { get; private set; }
    public long AttachmentTotalBytes { get; private set; }

    public EmailStatus Status { get; private set; } = EmailStatus.Queued;
    public DateTimeOffset? SentAtUtc { get; private set; }
    public string? ErrorMessage { get; private set; }

    public Guid? InvoiceId { get; private set; }

    public Guid? SenderUserId { get; private set; }
    public string? SenderUserName { get; private set; }

    private EmailOutboxEntry() { }

    public EmailOutboxEntry(string toAddresses, string? ccAddresses,
                             string subject, string bodyPlainText,
                             string fromAddress, string? fromName,
                             int attachmentCount, long attachmentTotalBytes,
                             Guid? invoiceId, Guid? senderUserId, string? senderUserName)
    {
        if (string.IsNullOrWhiteSpace(toAddresses)) throw new ArgumentException("At least one To address is required", nameof(toAddresses));
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject is required", nameof(subject));
        if (string.IsNullOrWhiteSpace(fromAddress)) throw new ArgumentException("FromAddress is required", nameof(fromAddress));

        ToAddresses = toAddresses.Trim();
        CcAddresses = string.IsNullOrWhiteSpace(ccAddresses) ? null : ccAddresses.Trim();
        Subject = subject.Trim();
        BodyPlainText = bodyPlainText ?? string.Empty;
        FromAddress = fromAddress.Trim();
        FromName = string.IsNullOrWhiteSpace(fromName) ? null : fromName.Trim();
        AttachmentCount = Math.Max(0, attachmentCount);
        AttachmentTotalBytes = Math.Max(0, attachmentTotalBytes);
        InvoiceId = invoiceId == Guid.Empty ? null : invoiceId;
        SenderUserId = senderUserId;
        SenderUserName = string.IsNullOrWhiteSpace(senderUserName) ? null : senderUserName!.Trim();
        Status = EmailStatus.Queued;
    }

    public void MarkSent()
    {
        Status = EmailStatus.Sent;
        SentAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string? errorMessage)
    {
        Status = EmailStatus.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage!.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
