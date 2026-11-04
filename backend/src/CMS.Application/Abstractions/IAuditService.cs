namespace CMS.Application.Abstractions;

/// <summary>
/// Explicit audit sink for behavior that doesn't produce a DbContext change — sensitive reads
/// and exports. Insert/update/delete audits are handled automatically by the SaveChanges interceptor.
/// </summary>
public interface IAuditService
{
    Task RecordSensitiveReadAsync(string entityType, string entityId, string context, CancellationToken ct = default);

    Task RecordExportAsync(string entityType, string context, int rowCount, object? filter = null, CancellationToken ct = default);
}
