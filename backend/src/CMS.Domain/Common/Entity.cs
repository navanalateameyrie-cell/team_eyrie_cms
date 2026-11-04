namespace CMS.Domain.Common;

/// <summary>Marker base for aggregate roots and entities. Business entities land here from Phase 2 onward.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAtUtc { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; protected set; }
}
