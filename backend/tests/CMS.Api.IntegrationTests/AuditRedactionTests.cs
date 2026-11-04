using System.Text.Json;
using CMS.Domain.Auditing;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class AuditRedactionTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public AuditRedactionTests(CmsApiFactory f) => _factory = f;

    private static JsonElement Parse(string? json) => JsonDocument.Parse(json!).RootElement.Clone();

    [Fact]
    public async Task Sensitive_Property_Is_Redacted_In_Create_NewValues()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var staff = new Staff("Redact Me", "redact.me@cms.local");
        db.Staff.Add(staff);
        await db.SaveChangesAsync();

        var audit = await db.AuditEntries
            .Where(a => a.EntityType == nameof(Staff) && a.EntityId == staff.Id.ToString() && a.Action == AuditAction.Create)
            .SingleAsync();

        var newJson = Parse(audit.NewValues!);
        newJson.GetProperty(nameof(Staff.Email)).GetString().Should().Be("***");
        newJson.GetProperty(nameof(Staff.FullName)).GetString().Should().Be("Redact Me");
    }

    [Fact]
    public async Task Sensitive_Property_Is_Redacted_In_Both_OldValues_And_NewValues_On_Update()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var staff = new Staff("Redact Pair", "pair.old@cms.local");
        db.Staff.Add(staff);
        await db.SaveChangesAsync();

        staff.UpdateProfile("Redact Pair", "pair.new@cms.local");  // email change
        await db.SaveChangesAsync();

        var audit = await db.AuditEntries
            .Where(a => a.EntityType == nameof(Staff) && a.EntityId == staff.Id.ToString() && a.Action == AuditAction.Update)
            .SingleAsync();

        Parse(audit.OldValues!).GetProperty(nameof(Staff.Email)).GetString().Should().Be("***");
        Parse(audit.NewValues!).GetProperty(nameof(Staff.Email)).GetString().Should().Be("***");

        // But the actual stored value in the Staff row is NOT redacted.
        var stored = await db.Staff.FindAsync(staff.Id);
        stored!.Email.Should().Be("pair.new@cms.local");
    }

    [Fact]
    public async Task Sensitive_Property_Is_Redacted_On_Delete()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var staff = new Staff("Redact Delete", "delete.redact@cms.local");
        db.Staff.Add(staff);
        await db.SaveChangesAsync();

        db.Staff.Remove(staff);
        await db.SaveChangesAsync();

        var audit = await db.AuditEntries
            .Where(a => a.EntityType == nameof(Staff) && a.EntityId == staff.Id.ToString() && a.Action == AuditAction.Delete)
            .SingleAsync();

        Parse(audit.OldValues!).GetProperty(nameof(Staff.Email)).GetString().Should().Be("***");
    }

    [Fact]
    public async Task Non_Sensitive_Change_To_Record_With_Sensitive_Field_Still_Redacts_That_Field()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var staff = new Staff("Only Name Changes", "only.name@cms.local");
        db.Staff.Add(staff);
        await db.SaveChangesAsync();

        staff.UpdateProfile("Only Name Changes v2", staff.Email);
        await db.SaveChangesAsync();

        var audit = await db.AuditEntries
            .Where(a => a.EntityType == nameof(Staff) && a.EntityId == staff.Id.ToString() && a.Action == AuditAction.Update)
            .SingleAsync();

        // Email isn't a changed field, but it still appears in Old/NewValues and must be redacted.
        Parse(audit.OldValues!).GetProperty(nameof(Staff.Email)).GetString().Should().Be("***");
        Parse(audit.NewValues!).GetProperty(nameof(Staff.Email)).GetString().Should().Be("***");
    }
}
