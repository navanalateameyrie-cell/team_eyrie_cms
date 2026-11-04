using CMS.Application.Clients;
using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Clients;

/// <summary>
/// Idempotent dev-startup seeder that applies the <see cref="ServiceSchemaDefaults"/> catalog to
/// any ServiceDefinition whose <c>RequiredFieldsJson</c> is null. Existing custom schemas aren't
/// overwritten.
/// </summary>
public static class ServiceSchemaSeeder
{
    public static async Task ApplyAsync(CmsDbContext db, ILogger? log = null, CancellationToken ct = default)
    {
        var services = await db.ServiceDefinitions.ToListAsync(ct);
        int touched = 0;
        foreach (var svc in services)
        {
            if (!string.IsNullOrWhiteSpace(svc.RequiredFieldsJson)) continue;
            if (!ServiceSchemaDefaults.Catalog.TryGetValue(svc.Code, out var schema)) continue;
            svc.SetSchema(
                ServiceFieldSerializer.WriteFieldDefs(schema.required),
                ServiceFieldSerializer.WriteFieldDefs(schema.optional));
            touched++;
        }
        if (touched > 0)
        {
            await db.SaveChangesAsync(ct);
            log?.LogInformation("ServiceSchemaSeeder: applied default schemas to {Count} services", touched);
        }
    }
}
