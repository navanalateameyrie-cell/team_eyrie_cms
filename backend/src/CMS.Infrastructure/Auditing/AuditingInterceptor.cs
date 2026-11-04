using System.Reflection;
using CMS.Application.Abstractions;
using CMS.Domain.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CMS.Infrastructure.Auditing;

/// <summary>
/// SaveChanges interceptor that materialises an <see cref="AuditEntry"/> row for every
/// insert/update/delete of an [Auditable] entity, in the same transaction as the write itself.
/// </summary>
public class AuditingInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _user;
    private readonly ICorrelationContext _correlation;
    private readonly IHttpContextAccessor _http;

    public AuditingInterceptor(ICurrentUser user, ICorrelationContext correlation, IHttpContextAccessor http)
    {
        _user = user;
        _correlation = correlation;
        _http = http;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) CaptureAudits(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is not null) CaptureAudits(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void CaptureAudits(DbContext ctx)
    {
        ctx.ChangeTracker.DetectChanges();

        var auditable = ctx.ChangeTracker.Entries()
            .Where(e => e.Entity.GetType().GetCustomAttribute<AuditableAttribute>() is not null)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (auditable.Count == 0) return;

        var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var ua = _http.HttpContext?.Request.Headers.UserAgent.ToString();
        var correlationId = string.IsNullOrWhiteSpace(_correlation.CorrelationId) ? null : _correlation.CorrelationId;
        var userId = _user.UserId;
        var userName = _user.UserName;

        var newRows = new List<AuditEntry>(capacity: auditable.Count);
        foreach (var entry in auditable)
        {
            var clrType = entry.Entity.GetType();
            var attr = clrType.GetCustomAttribute<AuditableAttribute>()!;
            var entityType = attr.Name ?? clrType.Name;
            var entityId = ReadEntityId(entry);

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Create,
                EntityState.Modified => AuditAction.Update,
                EntityState.Deleted => AuditAction.Delete,
                _ => AuditAction.Update,
            };

            string? oldJson = null, newJson = null;
            string? changedJson = null;

            if (entry.State == EntityState.Added)
            {
                newJson = AuditValueSerializer.SerializeNew(entry);
                changedJson = AuditValueSerializer.SerializeChangedFields(
                    entry.Properties.Where(p => !p.Metadata.IsShadowProperty())
                                    .Select(p => p.Metadata.Name));
            }
            else if (entry.State == EntityState.Modified)
            {
                var changed = entry.Properties.Where(p => p.IsModified && !p.Metadata.IsShadowProperty())
                                              .Select(p => p.Metadata.Name).ToArray();
                if (changed.Length == 0) continue;  // modified==no-op (e.g. same value re-assigned)
                oldJson = AuditValueSerializer.SerializeOld(entry);
                newJson = AuditValueSerializer.SerializeNew(entry);
                changedJson = AuditValueSerializer.SerializeChangedFields(changed);
            }
            else // Deleted
            {
                oldJson = AuditValueSerializer.SerializeOld(entry);
            }

            newRows.Add(AuditEntry.ForEntityChange(
                entityType: entityType,
                entityId: entityId,
                action: action,
                changedFieldsJson: changedJson,
                oldValuesJson: oldJson,
                newValuesJson: newJson,
                userId: userId,
                userName: userName,
                correlationId: correlationId,
                ipAddress: ip,
                userAgent: ua));
        }

        ctx.Set<AuditEntry>().AddRange(newRows);
    }

    private static string ReadEntityId(EntityEntry entry)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk is null) return string.Empty;
        var parts = new List<string>(pk.Properties.Count);
        foreach (var p in pk.Properties)
        {
            var v = entry.Property(p.Name).CurrentValue;
            parts.Add(v?.ToString() ?? "");
        }
        return parts.Count == 1 ? parts[0] : string.Join("|", parts);
    }
}
