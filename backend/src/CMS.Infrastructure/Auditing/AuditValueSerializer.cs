using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using CMS.Domain.Auditing;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CMS.Infrastructure.Auditing;

/// <summary>
/// Builds the OldValues / NewValues / ChangedFields JSON payloads for an EF entry while applying
/// property-level redaction driven by <see cref="SensitiveAuditAttribute"/>.
/// </summary>
public static class AuditValueSerializer
{
    private static readonly ConcurrentDictionary<Type, HashSet<string>> SensitiveNamesByType = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string? SerializeOld(EntityEntry entry)
    {
        var sensitive = GetSensitiveNames(entry.Entity.GetType());
        var dict = new Dictionary<string, object?>();
        foreach (var p in entry.Properties)
        {
            if (p.Metadata.IsShadowProperty()) continue;
            dict[p.Metadata.Name] = sensitive.Contains(p.Metadata.Name) ? "***" : p.OriginalValue;
        }
        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict, JsonOpts);
    }

    public static string? SerializeNew(EntityEntry entry)
    {
        var sensitive = GetSensitiveNames(entry.Entity.GetType());
        var dict = new Dictionary<string, object?>();
        foreach (var p in entry.Properties)
        {
            if (p.Metadata.IsShadowProperty()) continue;
            dict[p.Metadata.Name] = sensitive.Contains(p.Metadata.Name) ? "***" : p.CurrentValue;
        }
        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict, JsonOpts);
    }

    public static string SerializeChangedFields(IEnumerable<string> names)
        => JsonSerializer.Serialize(names.ToArray(), JsonOpts);

    public static string SerializeFilter(object? filter)
        => filter is null ? "null" : JsonSerializer.Serialize(filter, JsonOpts);

    private static HashSet<string> GetSensitiveNames(Type clrType) =>
        SensitiveNamesByType.GetOrAdd(clrType, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(pi => pi.GetCustomAttribute<SensitiveAuditAttribute>() is not null)
             .Select(pi => pi.Name)
             .ToHashSet(StringComparer.Ordinal));
}
