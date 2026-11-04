namespace CMS.Domain.Auditing;

/// <summary>
/// Append-only audit record. The entity has no public setters past construction;
/// EF reads/writes via private fields through configured conventions.
/// </summary>
public class AuditEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public AuditAction Action { get; private set; }

    /// <summary>JSON array of property names that changed (empty for Read/Export; all properties for Create).</summary>
    public string? ChangedFields { get; private set; }
    /// <summary>JSON document of pre-change values (null for Create/Read/Export).</summary>
    public string? OldValues { get; private set; }
    /// <summary>JSON document of post-change values (null for Delete).</summary>
    public string? NewValues { get; private set; }

    public Guid? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    /// <summary>Free-form label — e.g. "SensitiveRead:Client.ViewAadhaar" or "Export:ClientList".</summary>
    public string? Context { get; private set; }

    private AuditEntry() { }

    public static AuditEntry ForEntityChange(
        string entityType,
        string entityId,
        AuditAction action,
        string? changedFieldsJson,
        string? oldValuesJson,
        string? newValuesJson,
        Guid? userId,
        string? userName,
        string? correlationId,
        string? ipAddress,
        string? userAgent)
    {
        if (action == AuditAction.Read || action == AuditAction.Export)
            throw new ArgumentException("Use ForSensitiveRead/ForExport for read-shaped audits.", nameof(action));

        return new AuditEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ChangedFields = changedFieldsJson,
            OldValues = oldValuesJson,
            NewValues = newValuesJson,
            UserId = userId,
            UserName = userName,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };
    }

    public static AuditEntry ForSensitiveRead(
        string entityType, string entityId, string context,
        Guid? userId, string? userName, string? correlationId, string? ipAddress, string? userAgent)
        => new()
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = AuditAction.Read,
            Context = context,
            UserId = userId,
            UserName = userName,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };

    public static AuditEntry ForExport(
        string entityType, string context, int rowCount, string? filterJson,
        Guid? userId, string? userName, string? correlationId, string? ipAddress, string? userAgent)
        => new()
        {
            EntityType = entityType,
            EntityId = $"rows:{rowCount}",
            Action = AuditAction.Export,
            Context = context,
            NewValues = filterJson,
            UserId = userId,
            UserName = userName,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };
}
