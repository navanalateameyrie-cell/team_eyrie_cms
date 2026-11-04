using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.ServiceFilesTestFixtures;

/// <summary>
/// Enforces the Phase 7 invariant: service files have independent lifecycle. Closing a file never
/// changes the client or branch; deactivating a client or closing its only branch never changes
/// the files. Also pins the "no business columns on Client" guarantee — client row shape is unchanged.
/// </summary>
namespace CMS.Api.IntegrationTests;

public class ServiceFilesIndependenceTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ServiceFilesIndependenceTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Closing_File_Does_Not_Deactivate_Client_Or_Branch()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, branchId) = await SeedClientWithServiceAsync(c, withBranch: true);
        var cr = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, branchId, new DateOnly(2024, 4, 1), null, null));
        var file = (await cr.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        await c.PostAsync($"/api/v1/clients/{clientId}/service-files/{file.Id}/close?version={file.Version}", null);

        var client = await c.GetFromJsonAsync<ClientsController.ClientDto>($"/api/v1/clients/{clientId}");
        client!.IsActive.Should().BeTrue();

        var branch = await c.GetFromJsonAsync<ClientBranchesController.BranchDto>(
            $"/api/v1/clients/{clientId}/branches/{branchId}");
        branch!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivating_Client_Leaves_File_Open()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await SeedClientWithServiceAsync(c);
        var cr = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 4, 1), null, null));
        var file = (await cr.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        var client = await c.GetFromJsonAsync<ClientsController.ClientDto>($"/api/v1/clients/{clientId}");
        var deact = await c.PostAsync($"/api/v1/clients/{clientId}/deactivate?version={client!.Version}", null);
        deact.StatusCode.Should().Be(HttpStatusCode.OK);

        var fileAfter = await c.GetFromJsonAsync<ClientServiceFilesController.ServiceFileDto>(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}");
        fileAfter!.Status.Should().Be(ServiceFileStatus.Open);
        fileAfter.ClosedOn.Should().BeNull();
    }

    [Fact]
    public async Task Closing_Only_Branch_Leaves_File_Open()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, svcId, _, branchId) = await SeedClientWithServiceAsync(c, withBranch: true);
        var cr = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, branchId, new DateOnly(2024, 4, 1), null, null));
        var file = (await cr.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        var branch = await c.GetFromJsonAsync<ClientBranchesController.BranchDto>(
            $"/api/v1/clients/{clientId}/branches/{branchId}");
        var close = await c.PostAsync(
            $"/api/v1/clients/{clientId}/branches/{branchId}/close?version={branch!.Version}", null);
        close.StatusCode.Should().Be(HttpStatusCode.OK);

        var fileAfter = await c.GetFromJsonAsync<ClientServiceFilesController.ServiceFileDto>(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}");
        fileAfter!.Status.Should().Be(ServiceFileStatus.Open);
    }

    [Fact]
    public async Task Client_Dto_Carries_No_Service_Columns_Just_Masters_And_Core_Fields()
    {
        // Guards the spec rule "do not add IT/GST/TDS/etc. columns to Client".
        var c = await AdminClientAsync(_factory);
        var (clientId, _, _, _) = await SeedClientWithServiceAsync(c);
        var resp = await c.GetAsync($"/api/v1/clients/{clientId}");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();

        var forbidden = new[] { "incomeTax", "gstin", "tds", "tcs", "roc", "serviceFlags" };
        foreach (var token in forbidden)
            json.Should().NotContain(token, $"Client DTO must not expose a '{token}' field — services belong on service_files.");
    }
}
