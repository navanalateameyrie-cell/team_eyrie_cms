using CMS.Domain.Common;

namespace CMS.Domain.Entities;

/// <summary>
/// In-app notification shown in the UI bell drawer. Appended once per event; the only mutation is
/// flipping <see cref="ReadAtUtc"/>. Keep bodies short — long content goes in the linked page.
/// </summary>
public class Notification : Entity
{
    public Guid UserId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? Link { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }

    private Notification() { }

    public Notification(Guid userId, string kind, string title, string body, string? link)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId required", nameof(userId));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Kind required", nameof(kind));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required", nameof(title));
        UserId = userId;
        Kind = kind.Trim();
        Title = title.Trim();
        Body = body?.Trim() ?? string.Empty;
        Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim();
    }

    public bool IsRead => ReadAtUtc.HasValue;

    public void MarkRead()
    {
        if (ReadAtUtc is not null) return;
        ReadAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
