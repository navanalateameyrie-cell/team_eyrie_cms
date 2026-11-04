using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers.Masters;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

/// <summary>
/// Exercises every master's happy-path CRUD + the duplicate-code conflict for the representative
/// EntityTypes resource. Each master's controller follows the same shape, so the sibling spot-checks
/// only verify list + create.
/// </summary>
public class MastersCrudTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public MastersCrudTests(CmsApiFactory f) => _factory = f;

    private async Task<HttpClient> AdminClientAsync()
    {
        var c = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task EntityTypes_Full_Crud_Roundtrip()
    {
        var client = await AdminClientAsync();
        var code = $"ET-{Guid.NewGuid():N}"[..10];

        // Create
        var createResp = await client.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto(code, "Partnership Firm", "Ind partnership", 10));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResp.Content.ReadFromJsonAsync<EntityTypeDto>())!;
        created.Code.Should().Be(code.ToUpperInvariant());

        // Get by id
        var getResp = await client.GetAsync($"/api/v1/masters/entity-types/{created.Id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // List and find it
        var listResp = await client.GetAsync($"/api/v1/masters/entity-types?search={code}");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await listResp.Content.ReadFromJsonAsync<EntityTypeDto[]>())!;
        list.Should().ContainSingle(x => x.Id == created.Id);

        // Update
        var updResp = await client.PutAsJsonAsync($"/api/v1/masters/entity-types/{created.Id}",
            new EntityTypeUpsertDto(code, "Partnership Firm (edited)", null, 20));
        updResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await updResp.Content.ReadFromJsonAsync<EntityTypeDto>())!;
        updated.Name.Should().Be("Partnership Firm (edited)");
        updated.DisplayOrder.Should().Be(20);

        // Deactivate
        var delResp = await client.DeleteAsync($"/api/v1/masters/entity-types/{created.Id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var postDel = await client.GetAsync($"/api/v1/masters/entity-types/{created.Id}");
        postDel.StatusCode.Should().Be(HttpStatusCode.OK);
        (await postDel.Content.ReadFromJsonAsync<EntityTypeDto>())!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task EntityTypes_Duplicate_Code_Returns_Conflict()
    {
        var client = await AdminClientAsync();
        var code = $"DUP-{Guid.NewGuid():N}"[..10];

        var first = await client.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto(code, "First", null));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await client.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto(code, "Second", null));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task States_Create_And_List()
    {
        var client = await AdminClientAsync();
        var code = $"S{Guid.NewGuid().ToString("N")[..3].ToUpper()}";
        var created = await client.PostAsJsonAsync("/api/v1/masters/states",
            new StateUpsertDto(code, "Karnataka", "IN", 1));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResp = await client.GetAsync($"/api/v1/masters/states?search={code}");
        listResp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Services_Create_And_Update()
    {
        var client = await AdminClientAsync();
        var code = $"SVC-{Guid.NewGuid():N}"[..10];
        var cr = await client.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto(code, "GST Filing", "Compliance", null, 10));
        cr.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await cr.Content.ReadFromJsonAsync<ServiceDto>())!;

        var upd = await client.PutAsJsonAsync($"/api/v1/masters/services/{dto.Id}",
            new ServiceUpsertDto(code, "GST Monthly Filing", "Compliance", "Monthly", 5));
        upd.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Staff_Create_With_Duplicate_Email_Returns_Conflict()
    {
        var client = await AdminClientAsync();
        var email = $"user{Guid.NewGuid():N}@cms.local";

        var first = await client.PostAsJsonAsync("/api/v1/masters/staff",
            new StaffUpsertDto("Jane A", email, null, null, "Associate"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await client.PostAsJsonAsync("/api/v1/masters/staff",
            new StaffUpsertDto("John B", email, null, null, "Analyst"));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ClientGroups_Create_And_Deactivate()
    {
        var client = await AdminClientAsync();
        var code = $"CG-{Guid.NewGuid():N}"[..8];
        var cr = await client.PostAsJsonAsync("/api/v1/masters/client-groups",
            new ClientGroupUpsertDto(code, "Family A", "Family group"));
        cr.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await cr.Content.ReadFromJsonAsync<ClientGroupDto>())!;

        var del = await client.DeleteAsync($"/api/v1/masters/client-groups/{dto.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
