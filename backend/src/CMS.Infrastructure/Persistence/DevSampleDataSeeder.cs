using CMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Persistence;

/// <summary>
/// Seeds essential master data (Entity Types, States, Services) in Development when the database
/// is empty, ensuring the practice masters are ready for Excel imports and manual client onboarding.
/// </summary>
public static class DevSampleDataSeeder
{
    public static async Task ApplyAsync(CmsDbContext db, ILogger? log = null, CancellationToken ct = default)
    {
        // 1. Seed Entity Types if empty
        if (!await db.EntityTypes.AnyAsync(ct))
        {
            db.EntityTypes.AddRange(
                new EntityType("PVT_LTD", "Private Limited Company", "Incorporated under Companies Act", 1),
                new EntityType("LLP", "Limited Liability Partnership", "Registered under LLP Act", 2),
                new EntityType("INDIVIDUAL", "Individual / Proprietorship", "Sole proprietor or professional", 3),
                new EntityType("PARTNERSHIP", "Partnership Firm", "Indian Partnership Act firm", 4),
                new EntityType("TRUST", "Trust / NGO", "Registered Charitable Trust / Society", 5)
            );
            await db.SaveChangesAsync(ct);
            log?.LogInformation("DevSampleDataSeeder: Seeded default entity types.");
        }

        // 2. Seed Indian States if empty
        if (!await db.States.AnyAsync(ct))
        {
            db.States.AddRange(
                new State("MH", "Maharashtra", "IN", 1),
                new State("DL", "Delhi", "IN", 2),
                new State("KA", "Karnataka", "IN", 3),
                new State("TN", "Tamil Nadu", "IN", 4),
                new State("GJ", "Gujarat", "IN", 5),
                new State("TS", "Telangana", "IN", 6),
                new State("WB", "West Bengal", "IN", 7),
                new State("UP", "Uttar Pradesh", "IN", 8)
            );
            await db.SaveChangesAsync(ct);
            log?.LogInformation("DevSampleDataSeeder: Seeded standard Indian states.");
        }

        // 3. Seed Service Definitions if empty
        if (!await db.ServiceDefinitions.AnyAsync(ct))
        {
            db.ServiceDefinitions.AddRange(
                new ServiceDefinition("GST", "Goods and Services Tax", "Taxation", "Monthly GSTR-1 and GSTR-3B filings", 1),
                new ServiceDefinition("IT", "Income Tax Filing & Audit", "Taxation", "Annual ITR and Section 44AB tax audit", 2),
                new ServiceDefinition("TDS", "Tax Deducted at Source", "Taxation", "Quarterly 24Q, 26Q return filings & Form 16", 3),
                new ServiceDefinition("AGST", "Annual GST Audit & Return", "Taxation", "GSTR-9 and GSTR-9C reconciliation", 4),
                new ServiceDefinition("ROC", "Company Law & MCA Filings", "Corporate", "AOC-4, MGT-7, and director KYC compliance", 5),
                new ServiceDefinition("BANKAUDIT", "Statutory Bank Audit", "Audit", "Concurrent and statutory bank branch audit", 6)
            );
            await db.SaveChangesAsync(ct);
            log?.LogInformation("DevSampleDataSeeder: Seeded standard service definitions.");
        }
    }
}
