using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class RbacTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public RbacTests(CmsApiFactory f) => _factory = f;

    private HttpClient AuthClient(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task Admin_Can_List_Permissions_Roles_And_Users()
    {
        var token = await _factory.LoginAsAdminAsync();
        var client = AuthClient(token);

        var permsRes = await client.GetAsync("/api/v1/rbac/permissions");
        permsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var perms = await permsRes.Content.ReadFromJsonAsync<List<RbacController.PermissionMetaDto>>();
        perms.Should().NotBeNull();
        perms!.Should().Contain(p => p.Id == "clients.read");
        perms.Should().Contain(p => p.Id == "users.manage");

        var rolesRes = await client.GetAsync("/api/v1/rbac/roles");
        rolesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var roles = await rolesRes.Content.ReadFromJsonAsync<List<RbacController.RoleDto>>();
        roles.Should().NotBeNull();
        roles!.Should().Contain(r => r.Name == "SystemAdmin");
        roles.Should().Contain(r => r.Name == "Manager");
        roles.Should().Contain(r => r.Name == "StaffUser");

        var usersRes = await client.GetAsync("/api/v1/rbac/users");
        usersRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await usersRes.Content.ReadFromJsonAsync<List<RbacController.UserRbacDto>>();
        users.Should().NotBeNull();
        users!.Should().Contain(u => u.Email == "admin@cms.local");
    }

    [Fact]
    public async Task Non_Admin_Cannot_Access_Rbac_Endpoints()
    {
        var token = await _factory.CreateUserAndLoginAsync("staff.rbac.test@cms.local", "StaffUser");
        var client = AuthClient(token);

        (await client.GetAsync("/api/v1/rbac/permissions")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/rbac/roles")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/rbac/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_Can_Create_Custom_Role_And_Update_Permissions()
    {
        var token = await _factory.LoginAsAdminAsync();
        var client = AuthClient(token);

        var createReq = new RbacController.CreateRoleRequest(
            Name: "TaxReviewerCustom",
            Description: "Specialized tax review role",
            Permissions: new[] { "clients.read", "compliance.read", "documents.read" });

        var createRes = await client.PostAsJsonAsync("/api/v1/rbac/roles", createReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<RbacController.RoleDto>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("TaxReviewerCustom");
        created.Permissions.Should().Contain("clients.read");

        // Update role
        var updateReq = new RbacController.UpdateRoleRequest(
            Description: "Updated tax review role",
            Permissions: new[] { "clients.read", "compliance.read", "documents.read", "reports.read" });

        var updateRes = await client.PutAsJsonAsync($"/api/v1/rbac/roles/{created.Id}", updateReq);
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateRes.Content.ReadFromJsonAsync<RbacController.RoleDto>();
        updated!.Permissions.Should().Contain("reports.read");

        // Delete custom role
        var delRes = await client.DeleteAsync($"/api/v1/rbac/roles/{created.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
