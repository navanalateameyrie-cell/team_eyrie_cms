using System.Net;
using System.Net.Http.Headers;
using CMS.Api.Controllers;
using CMS.Domain.Auditing;
using CMS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class AuditExportTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public AuditExportTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Export_Returns_Csv_With_Header_And_Records_An_Export_Audit()
    {
        var admin = await AdminClientAsync(_factory);
        // Create at least one entity → produces audit rows.
        await SeedClientAndMastersAsync(admin);

        var beforeExportCount = await CountExportsAsync();

        var res = await admin.GetAsync("/api/v1/audit/export");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        var text = await res.Content.ReadAsStringAsync();
        text.Should().StartWith("OccurredAtUtc,Action,EntityType,EntityId,UserName");
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCountGreaterThan(1,
            "the header line plus at least one audit row from the seed above");

        var afterExportCount = await CountExportsAsync();
        afterExportCount.Should().Be(beforeExportCount + 1, "the export itself must be audited");
    }

    [Fact]
    public async Task Export_Requires_Audit_Read_Permission()
    {
        var token = await _factory.CreateUserAndLoginAsync("docs-only@cms.local", "StaffUser");
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // StaffUser has no audit.read permission.
        var res = await c.GetAsync("/api/v1/audit/export");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<int> CountExportsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        return await db.AuditEntries.CountAsync(a => a.Action == AuditAction.Export && a.EntityType == "AuditEntry");
    }
}
