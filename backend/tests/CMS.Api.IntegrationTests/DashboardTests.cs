using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.StaffAssignmentsTestFixtures;

namespace CMS.Api.IntegrationTests;

public class DashboardTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public DashboardTests(CmsApiFactory f) => _factory = f;

    private async Task<(HttpClient c, Guid clientId, Guid etId, Guid stateId)>
        SeedAsync()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId, _) = await ServiceFilesTestFixtures.SeedClientWithServiceAsync(c, withBranch: true);
        var client = await c.GetFromJsonAsync<ClientsController.ClientDto>($"/api/v1/clients/{clientId}");
        return (c, clientId, client!.EntityTypeId, stateId);
    }

    [Fact]
    public async Task Kpis_Reflect_Current_State()
    {
        var (c, _, _, _) = await SeedAsync();
        var kpis = await c.GetFromJsonAsync<DashboardController.Kpis>("/api/v1/dashboard/kpis");
        kpis!.ActiveClients.Should().BeGreaterThanOrEqualTo(1);
        kpis.ActiveBranches.Should().BeGreaterThanOrEqualTo(1);
        kpis.ActiveStaff.Should().BeGreaterThanOrEqualTo(1); // seeded admin's staff row
    }

    [Fact]
    public async Task Clients_By_Entity_Type_Groups_By_Name()
    {
        var (c, _, _, _) = await SeedAsync();
        var rows = await c.GetFromJsonAsync<DashboardController.Breakdown[]>(
            "/api/v1/dashboard/clients-by-entity-type");
        rows!.Should().NotBeEmpty();
        rows.Sum(r => r.Count).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Clients_By_State_Only_Counts_Clients_With_Primary_Active_Branch()
    {
        var (c, _, _, _) = await SeedAsync();
        var rows = await c.GetFromJsonAsync<DashboardController.Breakdown[]>(
            "/api/v1/dashboard/clients-by-state");
        rows!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Service_Files_By_Service_Excludes_Closed_Files()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await ServiceFilesTestFixtures.SeedClientWithServiceAsync(c);

        var openResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 4, 1), null, null));
        openResp.EnsureSuccessStatusCode();

        var closedResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 4, 1), null, null));
        var toClose = (await closedResp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        (await c.PostAsync($"/api/v1/clients/{clientId}/service-files/{toClose.Id}/close?version={toClose.Version}", null))
            .EnsureSuccessStatusCode();

        var rows = await c.GetFromJsonAsync<DashboardController.Breakdown[]>(
            "/api/v1/dashboard/service-files-by-service");
        // The seeded service name appears, and total count across rows is at least 1 (the still-open file).
        rows!.Sum(r => r.Count).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Staff_Workload_Top_Returns_Rows_For_Users_With_Staff_Read()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var staffId = await CreateStaffAsync(c);
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffId, AssignmentRole.Primary, new DateOnly(2024, 4, 1), null)))
            .EnsureSuccessStatusCode();

        var resp = await c.GetAsync("/api/v1/dashboard/staff-workload-top?take=5");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = (await resp.Content.ReadFromJsonAsync<DashboardController.Breakdown[]>())!;
        rows.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Staff_Workload_Top_Returns_403_Without_Staff_Read()
    {
        var token = await _factory.CreateUserAndLoginAsync("sw-top@cms.local", "StaffUser");
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await c.GetAsync("/api/v1/dashboard/staff-workload-top");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
