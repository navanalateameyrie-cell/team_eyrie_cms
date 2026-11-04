using CMS.Application.Abstractions;
using CMS.Domain.Auditing;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace CMS.Infrastructure.Auditing;

public class AuditService : IAuditService
{
    private readonly CmsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ICorrelationContext _correlation;
    private readonly IHttpContextAccessor _http;

    public AuditService(CmsDbContext db, ICurrentUser user, ICorrelationContext correlation, IHttpContextAccessor http)
    {
        _db = db;
        _user = user;
        _correlation = correlation;
        _http = http;
    }

    public async Task RecordSensitiveReadAsync(string entityType, string entityId, string context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("entityType required", nameof(entityType));
        if (string.IsNullOrWhiteSpace(context)) throw new ArgumentException("context required", nameof(context));

        var entry = AuditEntry.ForSensitiveRead(
            entityType: entityType,
            entityId: entityId ?? string.Empty,
            context: context,
            userId: _user.UserId,
            userName: _user.UserName,
            correlationId: NullIfBlank(_correlation.CorrelationId),
            ipAddress: _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            userAgent: _http.HttpContext?.Request.Headers.UserAgent.ToString());

        _db.Set<AuditEntry>().Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordExportAsync(string entityType, string context, int rowCount, object? filter = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("entityType required", nameof(entityType));
        if (string.IsNullOrWhiteSpace(context)) throw new ArgumentException("context required", nameof(context));

        var entry = AuditEntry.ForExport(
            entityType: entityType,
            context: context,
            rowCount: rowCount,
            filterJson: AuditValueSerializer.SerializeFilter(filter),
            userId: _user.UserId,
            userName: _user.UserName,
            correlationId: NullIfBlank(_correlation.CorrelationId),
            ipAddress: _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            userAgent: _http.HttpContext?.Request.Headers.UserAgent.ToString());

        _db.Set<AuditEntry>().Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
