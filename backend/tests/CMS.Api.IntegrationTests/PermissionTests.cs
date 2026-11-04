using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class PermissionTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public PermissionTests(CmsApiFactory f) => _factory = f;

    private HttpClient AuthClient(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task Admin_Can_Reach_All_Permission_Protected_Endpoints()
    {
        var token = await _factory.LoginAsAdminAsync();
        var client = AuthClient(token);

        (await client.GetAsync("/api/v1/diagnostics/authenticated")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/diagnostics/clients-read")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/diagnostics/users-manage")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Manager_Has_ClientsRead_But_Not_UsersManage()
    {
        var token = await _factory.CreateUserAndLoginAsync("manager@cms.local", "Manager");
        var client = AuthClient(token);

        (await client.GetAsync("/api/v1/diagnostics/authenticated")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/diagnostics/clients-read")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/diagnostics/users-manage")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StaffUser_Has_ClientsRead_But_Not_UsersManage()
    {
        var token = await _factory.CreateUserAndLoginAsync("staff.user@cms.local", "StaffUser");
        var client = AuthClient(token);

        (await client.GetAsync("/api/v1/diagnostics/clients-read")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/diagnostics/users-manage")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task No_Token_Returns_401_On_Permission_Endpoints()
    {
        var client = _factory.CreateClient();
        (await client.GetAsync("/api/v1/diagnostics/authenticated")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/diagnostics/clients-read")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/diagnostics/users-manage")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
