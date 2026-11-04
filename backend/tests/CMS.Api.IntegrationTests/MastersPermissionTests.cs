using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers.Masters;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class MastersPermissionTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public MastersPermissionTests(CmsApiFactory f) => _factory = f;

    private HttpClient Bearer(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task Anonymous_Cannot_Read_Masters()
    {
        var c = _factory.CreateClient();
        var resp = await c.GetAsync("/api/v1/masters/entity-types");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Manager_Can_Read_But_Cannot_Write()
    {
        var token = await _factory.CreateUserAndLoginAsync("manager-m@cms.local", "Manager");
        var c = Bearer(token);

        (await c.GetAsync("/api/v1/masters/entity-types")).StatusCode.Should().Be(HttpStatusCode.OK);

        var post = await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto("TRY", "Try write", null));
        post.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StaffUser_Can_Read_Masters()
    {
        var token = await _factory.CreateUserAndLoginAsync("staff-m@cms.local", "StaffUser");
        var c = Bearer(token);

        (await c.GetAsync("/api/v1/masters/services")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await c.GetAsync("/api/v1/masters/states")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_Can_Write()
    {
        var c = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto($"ADM-{Guid.NewGuid():N}"[..9], "Admin created", null));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
