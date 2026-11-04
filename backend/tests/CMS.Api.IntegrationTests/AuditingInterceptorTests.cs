using System.Text.Json;
using CMS.Domain.Auditing;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class AuditingInterceptorTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public AuditingInterceptorTests(CmsApiFactory f) => _factory = f;

    private static JsonElement Parse(string? json) => JsonDocument.Parse(json!).RootElement.Clone();

    [Fact]
    public async Task Insert_Writes_Create_Audit_With_All_Fields_As_Changed_And_New_Values()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var staff = new Staff("Alice Example", "alice@cms.local");
        db.Staff.Add(staff);
        await db.SaveChangesAsync();

        var audit = (await db.AuditEntries
                .Where(a => a.EntityType == nameof(Staff) && a.EntityId == staff.Id.ToString())
                .ToListAsync())
            .OrderBy(a => a.OccurredAtUtc)
            .First();

        audit.Action.Should().Be(AuditAction.Create);
        audit.OldValues.Should().BeNull();
        audit.NewValues.Should().NotBeNullOrWhiteSpace();
        audit.ChangedFields.Should().NotBeNullOrWhiteSpace();

        var changed = Parse(audit.ChangedFields).EnumerateArray().Select(e => e.GetString()).ToArray();
        changed.Should().Contain(new[] { nameof(Staff.FullName), nameof(Staff.Email), nameof(Staff.IsActive), nameof(Staff.Id) });

        var newJson = Parse(audit.NewValues);
        newJson.GetProperty(nameof(Staff.FullName)).GetString().Should().Be("Alice Example");
    }

    [Fact]
    public async Task Update_Writes_Only_Changed_Fields_With_Old_And_New_Values()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var staff = new Staff("Bob Original", "bob.original@cms.local");
        db.Staff.Add(staff);
        await db.SaveChangesAsync();

        staff.UpdateProfile("Bob Renamed", staff.Email);  // email unchanged on purpose
        await db.SaveChangesAsync();

        var audit = (await db.AuditEntries
                .Where(a => a.EntityType == nameof(Staff) && a.EntityId == staff.Id.ToString() && a.Action == AuditAction.Update)
                .ToListAsync())
            .OrderBy(a => a.OccurredAtUtc)
            .First();

        var changed = Parse(audit.ChangedFields!).EnumerateArray().Select(e => e.GetString()).ToArray();
        changed.Should().Contain(nameof(Staff.FullName));
        changed.Should().Contain(nameof(Staff.UpdatedAtUtc));
        changed.Should().NotContain(nameof(Staff.Email));

        var oldJson = Parse(audit.OldValues!);
        var newJson = Parse(audit.NewValues!);
        oldJson.GetProperty(nameof(Staff.FullName)).GetString().Should().Be("Bob Original");
        newJson.GetProperty(nameof(Staff.FullName)).GetString().Should().Be("Bob Renamed");
    }

    [Fact]
    public async Task Delete_Writes_Delete_Audit_With_Old_Values_Only()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var staff = new Staff("Carol Transient", "carol@cms.local");
        db.Staff.Add(staff);
        await db.SaveChangesAsync();

        db.Staff.Remove(staff);
        await db.SaveChangesAsync();

        var audit = await db.AuditEntries
            .Where(a => a.EntityType == nameof(Staff) && a.EntityId == staff.Id.ToString() && a.Action == AuditAction.Delete)
            .SingleAsync();

        audit.OldValues.Should().NotBeNullOrWhiteSpace();
        audit.NewValues.Should().BeNull();

        var oldJson = Parse(audit.OldValues!);
        oldJson.GetProperty(nameof(Staff.FullName)).GetString().Should().Be("Carol Transient");
    }

    [Fact]
    public async Task Update_With_No_Property_Changes_Writes_No_Audit()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var staff = new Staff("Dave NoOp", "dave@cms.local");
        db.Staff.Add(staff);
        await db.SaveChangesAsync();

        var baselineCount = await db.AuditEntries
            .CountAsync(a => a.EntityId == staff.Id.ToString());

        // No-op save (nothing was changed on the entity).
        await db.SaveChangesAsync();

        var postCount = await db.AuditEntries.CountAsync(a => a.EntityId == staff.Id.ToString());
        postCount.Should().Be(baselineCount);
    }

    [Fact]
    public async Task Audit_Captures_User_And_Correlation_From_Ambient_Context()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var expectedUserId = Guid.NewGuid();
        var http = sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var fakeCtx = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        fakeCtx.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, expectedUserId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "ambient.user@cms.local"),
        }, authenticationType: "Test"));
        fakeCtx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.42");
        fakeCtx.Request.Headers.UserAgent = "AuditTests/1.0";
        http.HttpContext = fakeCtx;

        var corr = sp.GetRequiredService<CMS.Api.Middleware.CorrelationContextAccessor>();
        corr.CorrelationId = "corr-xyz-123";

        // Resolve DbContext AFTER ambient state is set, so the interceptor's scoped deps
        // (ICurrentUser, ICorrelationContext) materialise against the crafted context.
        var db = sp.GetRequiredService<CmsDbContext>();
        var staff = new Staff("Eve Ambient", "eve.ambient@cms.local");
        db.Staff.Add(staff);
        await db.SaveChangesAsync();

        var audit = await db.AuditEntries.SingleAsync(a => a.EntityId == staff.Id.ToString());
        audit.UserId.Should().Be(expectedUserId);
        audit.UserName.Should().Be("ambient.user@cms.local");
        audit.CorrelationId.Should().Be("corr-xyz-123");
        audit.IpAddress.Should().Be("10.0.0.42");
        audit.UserAgent.Should().Be("AuditTests/1.0");
    }
}
