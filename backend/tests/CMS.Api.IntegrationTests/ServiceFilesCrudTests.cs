using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.ServiceFilesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class ServiceFilesCrudTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ServiceFilesCrudTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Create_Assigns_10_Digit_FileNumber_And_Opens_The_File()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, serviceId, _, _) = await SeedClientWithServiceAsync(c);

        var resp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                ServiceDefinitionId: serviceId, BranchId: null,
                OpenedOn: new DateOnly(2024, 4, 1), LegacyFileNumber: null, Notes: null));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var dto = (await resp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        dto.FileNumber.Should().HaveLength(10);
        dto.FileNumber.Should().MatchRegex("^[0-9]{10}$");
        dto.Status.Should().Be(ServiceFileStatus.Open);
        dto.BranchId.Should().BeNull();
        dto.Version.Should().Be(1);
    }

    [Fact]
    public async Task Create_With_Branch_Requires_Branch_To_Belong_To_Same_Client()
    {
        var c = await AdminClientAsync(_factory);
        var (clientA, svcA, _, _) = await SeedClientWithServiceAsync(c, withBranch: false);
        var (_, _, _, foreignBranch) = await SeedClientWithServiceAsync(c, withBranch: true);

        var resp = await c.PostAsJsonAsync($"/api/v1/clients/{clientA}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                ServiceDefinitionId: svcA, BranchId: foreignBranch,
                OpenedOn: new DateOnly(2024, 4, 1), LegacyFileNumber: null, Notes: null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_With_Inactive_Service_Returns_400()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await SeedClientWithServiceAsync(c);

        (await c.DeleteAsync($"/api/v1/masters/services/{svcId}")).EnsureSuccessStatusCode();

        var resp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                ServiceDefinitionId: svcId, BranchId: null,
                OpenedOn: new DateOnly(2024, 4, 1), LegacyFileNumber: null, Notes: null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Legacy_File_Number_Scoped_Unique_Per_Client_And_Service()
    {
        // Phase 14A tightened the previously-global uniqueness to (clientId, serviceDefinitionId,
        // legacyFileNumber). Real-world CA data reuses legacy codes across clients.
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await SeedClientWithServiceAsync(c);
        var (otherClient, otherSvc, _, _) = await SeedClientWithServiceAsync(c);
        var legacy = $"LEGACY-{Guid.NewGuid():N}";

        var first = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 1, 1), legacy, null));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstDto = (await first.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        firstDto.LegacyFileNumber.Should().Be(legacy);

        // Same legacy on a DIFFERENT client is now allowed (business change).
        var crossClient = await c.PostAsJsonAsync($"/api/v1/clients/{otherClient}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                otherSvc, null, new DateOnly(2024, 1, 1), legacy, null));
        crossClient.StatusCode.Should().Be(HttpStatusCode.Created);

        // But same legacy on same (client, service) still conflicts.
        var sameScope = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 1, 1), legacy, null));
        sameScope.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_Bumps_Version_And_Rejects_Stale_Replay()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await SeedClientWithServiceAsync(c);
        var create = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 4, 1), null, "initial"));
        var dto = (await create.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        var upd = new ClientServiceFilesController.UpdateServiceFileRequest(
            svcId, null, null, "renamed note", dto.Version);
        var first = await c.PutAsJsonAsync($"/api/v1/clients/{clientId}/service-files/{dto.Id}", upd);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var stale = await c.PutAsJsonAsync($"/api/v1/clients/{clientId}/service-files/{dto.Id}", upd);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task List_Filters_By_Status_And_Search()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await SeedClientWithServiceAsync(c);
        var mkResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 4, 1), null, "alpha"));
        var dto = (await mkResp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        var byNumber = await c.GetFromJsonAsync<ClientServiceFilesController.ServiceFileDto[]>(
            $"/api/v1/clients/{clientId}/service-files?search={dto.FileNumber}");
        byNumber!.Should().ContainSingle(x => x.Id == dto.Id);

        var openOnly = await c.GetFromJsonAsync<ClientServiceFilesController.ServiceFileDto[]>(
            $"/api/v1/clients/{clientId}/service-files?status=Open");
        openOnly!.Should().Contain(x => x.Id == dto.Id);
    }

    [Fact]
    public async Task Global_Search_Finds_File_Across_Clients()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await SeedClientWithServiceAsync(c);
        var mkResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 4, 1), null, null));
        var dto = (await mkResp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        var found = await c.GetFromJsonAsync<ClientServiceFilesController.ServiceFileDto[]>(
            $"/api/v1/service-files?search={dto.FileNumber}");
        found!.Should().ContainSingle(x => x.Id == dto.Id);
    }

    [Fact]
    public async Task Generated_File_Numbers_Are_Unique()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await SeedClientWithServiceAsync(c);
        var numbers = new HashSet<string>();
        for (int i = 0; i < 10; i++)
        {
            var r = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
                new ClientServiceFilesController.CreateServiceFileRequest(
                    svcId, null, new DateOnly(2024, 4, 1), null, null));
            var dto = (await r.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
            numbers.Add(dto.FileNumber).Should().BeTrue();
        }
    }
}
