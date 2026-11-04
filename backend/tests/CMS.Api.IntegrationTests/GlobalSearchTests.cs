using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class GlobalSearchTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public GlobalSearchTests(CmsApiFactory f) => _factory = f;

    private async Task<HttpClient> AdminClientAsync()
    {
        var c = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static async Task<(Guid clientId, Guid branchId, Guid fileId, ClientsController.ClientDto client,
                              ClientBranchesController.BranchDto branch, ClientServiceFilesController.ServiceFileDto file)>
        SeedRichClientAsync(HttpClient c, string? legacyClientId = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();

        var et = (await (await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto($"GS-{suffix}", $"EntityType {suffix}", null)))
            .Content.ReadFromJsonAsync<EntityTypeDto>())!;
        var st = (await (await c.PostAsJsonAsync("/api/v1/masters/states",
            new StateUpsertDto($"ST{suffix}", $"State {suffix}", "IN", 0)))
            .Content.ReadFromJsonAsync<StateDto>())!;
        var svc = (await (await c.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto($"SV-{suffix}", $"Service {suffix}", null, null, 0)))
            .Content.ReadFromJsonAsync<ServiceDto>())!;

        var client = (await (await c.PostAsJsonAsync("/api/v1/clients",
            new ClientsController.CreateClientRequest(
                $"Acme Industries {suffix}", et.Id, $"Acme {suffix}", "AAAAA1111A",
                null, legacyClientId, AcknowledgeDuplicatePan: true)))
            .Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        var branch = (await (await c.PostAsJsonAsync($"/api/v1/clients/{client.Id}/branches",
            new ClientBranchesController.CreateBranchRequest(
                $"BR-{suffix}", "Head Office", BranchType.HeadOffice, st.Id,
                new ClientBranchesController.AddressDto("1 Infosys Rd", null, "Bengaluru", "560001"),
                new DateOnly(2024, 1, 1), true, true)))
            .Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        var file = (await (await c.PostAsJsonAsync($"/api/v1/clients/{client.Id}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svc.Id, branch.Id, new DateOnly(2024, 4, 1), $"LEG-{suffix}", null)))
            .Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        return (client.Id, branch.Id, file.Id, client, branch, file);
    }

    [Fact]
    public async Task Exact_Client_Code_Returns_The_Client()
    {
        var c = await AdminClientAsync();
        var (_, _, _, client, _, _) = await SeedRichClientAsync(c);

        var resp = await c.GetFromJsonAsync<SearchController.SearchResponse>(
            $"/api/v1/search?q={client.Code}&kind=Client");
        resp!.Items.Should().ContainSingle();
        var hit = resp.Items[0];
        hit.Kind.Should().Be(SearchController.HitKind.Client);
        hit.Id.Should().Be(client.Id);
        hit.MatchedOn.Should().Be("code");
    }

    [Fact]
    public async Task Exact_File_Number_Returns_The_File()
    {
        var c = await AdminClientAsync();
        var (_, _, _, _, _, file) = await SeedRichClientAsync(c);
        var resp = await c.GetFromJsonAsync<SearchController.SearchResponse>(
            $"/api/v1/search?q={file.FileNumber}");
        resp!.Items.Should().Contain(h => h.Kind == SearchController.HitKind.ServiceFile && h.Id == file.Id);
    }

    [Fact]
    public async Task Legacy_File_Number_Partial_Match_Hits()
    {
        var c = await AdminClientAsync();
        var (_, _, _, _, _, file) = await SeedRichClientAsync(c);
        var legacy = file.LegacyFileNumber!;
        var resp = await c.GetFromJsonAsync<SearchController.SearchResponse>(
            $"/api/v1/search?q={legacy[..6]}");
        resp!.Items.Should().Contain(h => h.Kind == SearchController.HitKind.ServiceFile && h.Id == file.Id);
    }

    [Fact]
    public async Task Legacy_Client_Id_Partial_Match_Hits()
    {
        var c = await AdminClientAsync();
        var legacy = $"OLD-{Guid.NewGuid():N}";
        var (clientId, _, _, _, _, _) = await SeedRichClientAsync(c, legacy);

        var resp = await c.GetFromJsonAsync<SearchController.SearchResponse>(
            $"/api/v1/search?q={legacy[..8]}");
        resp!.Items.Should().Contain(h => h.Kind == SearchController.HitKind.Client && h.Id == clientId);
    }

    [Fact]
    public async Task Trade_Name_Substring_Hits_Client()
    {
        var c = await AdminClientAsync();
        var (clientId, _, _, _, _, _) = await SeedRichClientAsync(c);
        var resp = await c.GetFromJsonAsync<SearchController.SearchResponse>("/api/v1/search?q=acme");
        resp!.Items.Should().Contain(h => h.Kind == SearchController.HitKind.Client && h.Id == clientId);
    }

    [Fact]
    public async Task Gstin_Hits_Through_Tax_Registration()
    {
        var c = await AdminClientAsync();
        var (clientId, branchId, _, _, _, _) = await SeedRichClientAsync(c);
        var gstin = "29ABCDE1234F1Z5";
        (await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Gstin, gstin, null, null)))
            .EnsureSuccessStatusCode();

        var resp = await c.GetFromJsonAsync<SearchController.SearchResponse>(
            $"/api/v1/search?q={gstin}");
        resp!.Items.Should().Contain(h => h.Kind == SearchController.HitKind.TaxRegistration);
    }

    [Fact]
    public async Task Pan_Match_Returns_Client_With_Matching_Pan()
    {
        var c = await AdminClientAsync();
        var (clientId, _, _, _, _, _) = await SeedRichClientAsync(c);
        var resp = await c.GetFromJsonAsync<SearchController.SearchResponse>("/api/v1/search?q=AAAAA1111A");
        resp!.Items.Should().Contain(h => h.Kind == SearchController.HitKind.Client && h.Id == clientId
                                          && h.MatchedOn == "pan");
    }

    [Fact]
    public async Task Pagination_And_Kind_Filter_Work()
    {
        var c = await AdminClientAsync();
        // seed two clients to produce multiple hits
        await SeedRichClientAsync(c);
        await SeedRichClientAsync(c);

        var paged = await c.GetFromJsonAsync<SearchController.SearchResponse>(
            "/api/v1/search?q=acme&skip=0&take=1");
        paged!.Items.Should().HaveCount(1);
        paged.Total.Should().BeGreaterThanOrEqualTo(2);

        var clientsOnly = await c.GetFromJsonAsync<SearchController.SearchResponse>(
            "/api/v1/search?q=acme&kind=Client");
        clientsOnly!.Items.Should().OnlyContain(h => h.Kind == SearchController.HitKind.Client);
    }

    [Fact]
    public async Task Empty_Query_Returns_Empty_Response_200()
    {
        var c = await AdminClientAsync();
        var resp = await c.GetAsync("/api/v1/search?q=");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await resp.Content.ReadFromJsonAsync<SearchController.SearchResponse>())!;
        body.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Anonymous_Search_Returns_401()
    {
        var c = _factory.CreateClient();
        var resp = await c.GetAsync("/api/v1/search?q=acme");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
