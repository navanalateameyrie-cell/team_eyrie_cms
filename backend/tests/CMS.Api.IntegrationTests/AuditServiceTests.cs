using System.Text.Json;
using CMS.Application.Abstractions;
using CMS.Domain.Auditing;
using CMS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class AuditServiceTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public AuditServiceTests(CmsApiFactory f) => _factory = f;

    private static (IServiceScope Scope, Guid UserId) BuildScopeWithUser(CmsApiFactory factory, string userName)
    {
        var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var http = sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var userId = Guid.NewGuid();
        var ctx = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        ctx.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, userName),
        }, authenticationType: "Test"));
        http.HttpContext = ctx;

        var corr = sp.GetRequiredService<CMS.Api.Middleware.CorrelationContextAccessor>();
        corr.CorrelationId = "corr-audit-service";

        return (scope, userId);
    }

    [Fact]
    public async Task RecordSensitiveReadAsync_Writes_Read_Audit_Row()
    {
        var (scope, userId) = BuildScopeWithUser(_factory, "reader@cms.local");
        using var _ = scope;

        var sp = scope.ServiceProvider;
        var svc = sp.GetRequiredService<IAuditService>();
        var db = sp.GetRequiredService<CmsDbContext>();

        var entityId = Guid.NewGuid().ToString();
        await svc.RecordSensitiveReadAsync("Client", entityId, "Client.ViewAadhaar");

        var row = await db.AuditEntries.SingleAsync(a => a.EntityId == entityId && a.Action == AuditAction.Read);
        row.EntityType.Should().Be("Client");
        row.Context.Should().Be("Client.ViewAadhaar");
        row.UserId.Should().Be(userId);
        row.UserName.Should().Be("reader@cms.local");
        row.CorrelationId.Should().Be("corr-audit-service");
        row.OldValues.Should().BeNull();
        row.NewValues.Should().BeNull();
        row.ChangedFields.Should().BeNull();
    }

    [Fact]
    public async Task RecordExportAsync_Writes_Export_Row_With_Filter_And_RowCount()
    {
        var (scope, _) = BuildScopeWithUser(_factory, "exporter@cms.local");
        using var _d = scope;

        var sp = scope.ServiceProvider;
        var svc = sp.GetRequiredService<IAuditService>();
        var db = sp.GetRequiredService<CmsDbContext>();

        var contextLabel = $"Export:StaffList:{Guid.NewGuid():N}";
        await svc.RecordExportAsync("Staff", contextLabel, rowCount: 42, filter: new { active = true, year = 2026 });

        var row = await db.AuditEntries.SingleAsync(a => a.Context == contextLabel && a.Action == AuditAction.Export);
        row.EntityType.Should().Be("Staff");
        row.EntityId.Should().Be("rows:42");
        row.NewValues.Should().NotBeNullOrWhiteSpace();

        var filterJson = JsonDocument.Parse(row.NewValues!).RootElement;
        filterJson.GetProperty("active").GetBoolean().Should().BeTrue();
        filterJson.GetProperty("year").GetInt32().Should().Be(2026);
    }

    [Fact]
    public async Task Service_Rejects_Blank_Required_Arguments()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAuditService>();

        await Assert.ThrowsAsync<ArgumentException>(() => svc.RecordSensitiveReadAsync("", "id", "ctx"));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.RecordSensitiveReadAsync("Entity", "id", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.RecordExportAsync("", "ctx", 0));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.RecordExportAsync("Entity", "", 0));
    }
}
