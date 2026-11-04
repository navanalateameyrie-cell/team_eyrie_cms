using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class ClientsCrudTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ClientsCrudTests(CmsApiFactory f) => _factory = f;

    private async Task<HttpClient> AdminClientAsync()
    {
        var c = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static async Task<Guid> EnsureEntityTypeAsync(HttpClient c, string code)
    {
        var create = await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto(code, $"ET {code}", null));
        if (create.StatusCode == HttpStatusCode.Conflict)
        {
            var list = await c.GetFromJsonAsync<EntityTypeDto[]>($"/api/v1/masters/entity-types?search={code}");
            return list!.First(x => x.Code == code).Id;
        }
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<EntityTypeDto>())!.Id;
    }

    [Fact]
    public async Task Create_Assigns_8_Digit_Code_And_Starts_Active()
    {
        var c = await AdminClientAsync();
        var etId = await EnsureEntityTypeAsync(c, $"ET{Guid.NewGuid().ToString("N")[..4].ToUpper()}");

        var resp = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            LegalName: "Acme Industries LLP", EntityTypeId: etId,
            TradeName: "Acme", Pan: null, ClientGroupId: null, LegacyClientId: null));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var dto = (await resp.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;
        dto.Code.Should().HaveLength(8);
        dto.Code.Should().MatchRegex("^[0-9]{8}$");
        dto.IsActive.Should().BeTrue();
        dto.Version.Should().Be(1);
    }

    [Fact]
    public async Task List_Finds_Client_By_Code_And_LegalName_Search()
    {
        var c = await AdminClientAsync();
        var etId = await EnsureEntityTypeAsync(c, $"ET{Guid.NewGuid().ToString("N")[..4].ToUpper()}");
        var legal = $"Zenith Capital Pvt Ltd {Guid.NewGuid():N}"[..40];

        var create = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            legal, etId, null, null, null, null));
        create.EnsureSuccessStatusCode();
        var dto = (await create.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        var byCode = await c.GetFromJsonAsync<ClientsController.ClientListResponse>($"/api/v1/clients?search={dto.Code}");
        byCode!.Items.Should().ContainSingle(x => x.Id == dto.Id);

        var byName = await c.GetFromJsonAsync<ClientsController.ClientListResponse>($"/api/v1/clients?search=Zenith");
        byName!.Items.Should().Contain(x => x.Id == dto.Id);
    }

    [Fact]
    public async Task Update_Bumps_Version_And_Preserves_Code()
    {
        var c = await AdminClientAsync();
        var etId = await EnsureEntityTypeAsync(c, $"ET{Guid.NewGuid().ToString("N")[..4].ToUpper()}");
        var create = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Original Name", etId, null, null, null, null));
        var dto = (await create.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        var upd = await c.PutAsJsonAsync($"/api/v1/clients/{dto.Id}", new ClientsController.UpdateClientRequest(
            LegalName: "Renamed Co", EntityTypeId: etId,
            TradeName: null, Pan: null, ClientGroupId: null, LegacyClientId: null,
            Version: dto.Version));
        upd.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = (await upd.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;
        after.Code.Should().Be(dto.Code);
        after.LegalName.Should().Be("Renamed Co");
        after.Version.Should().Be(dto.Version + 1);
    }

    [Fact]
    public async Task Deactivate_Then_Reactivate_Preserves_Code()
    {
        var c = await AdminClientAsync();
        var etId = await EnsureEntityTypeAsync(c, $"ET{Guid.NewGuid().ToString("N")[..4].ToUpper()}");
        var create = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Lifecycle Co", etId, null, null, null, null));
        var dto = (await create.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;
        var originalCode = dto.Code;

        var deact = await c.PostAsync($"/api/v1/clients/{dto.Id}/deactivate?version={dto.Version}", null);
        deact.StatusCode.Should().Be(HttpStatusCode.OK);
        var deactDto = (await deact.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;
        deactDto.IsActive.Should().BeFalse();
        deactDto.Code.Should().Be(originalCode);

        var react = await c.PostAsync($"/api/v1/clients/{dto.Id}/reactivate?version={deactDto.Version}", null);
        react.StatusCode.Should().Be(HttpStatusCode.OK);
        var reactDto = (await react.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;
        reactDto.IsActive.Should().BeTrue();
        reactDto.Code.Should().Be(originalCode); // same code — not re-generated
    }

    [Fact]
    public async Task Duplicate_LegacyClientId_Returns_409()
    {
        var c = await AdminClientAsync();
        var etId = await EnsureEntityTypeAsync(c, $"ET{Guid.NewGuid().ToString("N")[..4].ToUpper()}");
        var legacy = $"LEG-{Guid.NewGuid():N}";

        var first = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "First", etId, null, null, null, legacy));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Second", etId, null, null, null, legacy));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetByCode_Returns_Client()
    {
        var c = await AdminClientAsync();
        var etId = await EnsureEntityTypeAsync(c, $"ET{Guid.NewGuid().ToString("N")[..4].ToUpper()}");
        var create = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "FetchByCode", etId, null, null, null, null));
        var dto = (await create.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        var got = await c.GetFromJsonAsync<ClientsController.ClientDto>($"/api/v1/clients/by-code/{dto.Code}");
        got!.Id.Should().Be(dto.Id);
    }
}
