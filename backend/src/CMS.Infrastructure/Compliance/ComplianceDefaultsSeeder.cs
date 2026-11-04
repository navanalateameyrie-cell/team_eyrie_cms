using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Compliance;

/// <summary>
/// Idempotent dev-startup seeder that applies default compliance cadences to standard services
/// whose cadence is still null. Doesn't overwrite anything the practice has customised.
/// </summary>
public static class ComplianceDefaultsSeeder
{
    /// <summary>Service code → (cadence, days after period end when due).</summary>
    public static readonly IReadOnlyDictionary<string, (ComplianceFrequency Freq, int Offset)> Defaults =
        new Dictionary<string, (ComplianceFrequency, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["GST"]           = (ComplianceFrequency.Monthly, 20),   // GSTR-3B: 20th of next month
            ["AGST"]          = (ComplianceFrequency.Annual, 275),   // GSTR-9: 31-Dec after FY end
            ["TDS"]           = (ComplianceFrequency.Quarterly, 31), // Return by end of month after Q
            ["IT"]            = (ComplianceFrequency.Annual, 122),   // ITR: ~31-Jul after FY end
            ["CERTIFICATION"] = (ComplianceFrequency.Annual, 180),
            ["ROC"]           = (ComplianceFrequency.Annual, 180),   // AOC-4 / MGT-7
            ["PT"]            = (ComplianceFrequency.Monthly, 15),
            ["ESIPF"]         = (ComplianceFrequency.Monthly, 15),
            ["BANKAUDIT"]     = (ComplianceFrequency.Annual, 60),
            ["ACCOUNTS"]      = (ComplianceFrequency.Annual, 180),
        };

    public static async Task ApplyAsync(CmsDbContext db, ILogger? log = null, CancellationToken ct = default)
    {
        var services = await db.ServiceDefinitions.ToListAsync(ct);
        int touched = 0;
        foreach (var svc in services)
        {
            if (svc.Frequency is not null) continue; // respect existing customisation
            if (!Defaults.TryGetValue(svc.Code, out var def)) continue;
            svc.SetCompliance(def.Freq, def.Offset);
            touched++;
        }
        if (touched > 0)
        {
            await db.SaveChangesAsync(ct);
            log?.LogInformation("ComplianceDefaultsSeeder: applied default cadences to {Count} services", touched);
        }
    }
}
