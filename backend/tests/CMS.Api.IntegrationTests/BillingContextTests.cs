using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class BillingContextTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public BillingContextTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Branch_Billing_Context_Carries_Legal_Address_State_Pan_Gstin_And_Active_Files()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);

        // Give the client a PAN.
        var clientGet = await c.GetFromJsonAsync<ClientsController.ClientDto>($"/api/v1/clients/{clientId}");
        var updResp = await c.PutAsJsonAsync($"/api/v1/clients/{clientId}", new ClientsController.UpdateClientRequest(
            LegalName: clientGet!.LegalName,
            EntityTypeId: clientGet.EntityTypeId,
            TradeName: "Trade X",
            Pan: "ABCDE1234F",
            ClientGroupId: null, LegacyClientId: null,
            Version: clientGet.Version,
            AcknowledgeDuplicatePan: true));
        updResp.EnsureSuccessStatusCode();

        var brResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId));
        var br = (await brResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        // Add a GSTIN for the branch.
        (await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{br.Id}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Gstin, "29ABCDE1234F1Z5", null, null))).EnsureSuccessStatusCode();

        // Add an active service file at branch scope and a client-level file.
        // We need a service master row — reuse SeedClientWithServiceAsync's suffix pattern by using
        // the existing seed. The simpler path here: create a Service master directly.
        var svcResp = await c.PostAsJsonAsync("/api/v1/masters/services",
            new CMS.Api.Controllers.Masters.ServiceUpsertDto(
                $"BC-{Guid.NewGuid().ToString("N")[..4].ToUpper()}", "Billing Svc", null, null, 0));
        var svc = (await svcResp.Content.ReadFromJsonAsync<CMS.Api.Controllers.Masters.ServiceDto>())!;

        var fileAtBranch = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svc.Id, br.Id, new DateOnly(2024, 4, 1), null, null));
        fileAtBranch.EnsureSuccessStatusCode();
        var fileClientLevel = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svc.Id, null, new DateOnly(2024, 4, 1), null, null));
        fileClientLevel.EnsureSuccessStatusCode();

        var dto = await c.GetFromJsonAsync<BillingContextController.BillingContextDto>(
            $"/api/v1/clients/{clientId}/branches/{br.Id}/billing-context");
        dto!.LegalName.Should().Be(clientGet.LegalName);
        dto.TradeName.Should().Be("Trade X");
        dto.Pan.Should().Be("ABCDE1234F");
        dto.BranchCode.Should().Be(br.Code);
        dto.BranchIsBillingEnabled.Should().BeTrue();
        dto.StateName.Should().NotBeNullOrWhiteSpace();
        dto.Gstin.Should().Be("29ABCDE1234F1Z5");
        dto.Address.Line1.Should().NotBeNullOrWhiteSpace();
        dto.ActiveServiceFiles.Should().HaveCount(2);  // one branch-scoped + one client-level
    }

    [Fact]
    public async Task Branch_Billing_Context_Excludes_Closed_Files()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var brResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId));
        var br = (await brResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        var svcResp = await c.PostAsJsonAsync("/api/v1/masters/services",
            new CMS.Api.Controllers.Masters.ServiceUpsertDto(
                $"BC2-{Guid.NewGuid().ToString("N")[..4].ToUpper()}", "Svc 2", null, null, 0));
        var svc = (await svcResp.Content.ReadFromJsonAsync<CMS.Api.Controllers.Masters.ServiceDto>())!;

        var fileResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svc.Id, br.Id, new DateOnly(2024, 4, 1), null, null));
        var file = (await fileResp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        (await c.PostAsync($"/api/v1/clients/{clientId}/service-files/{file.Id}/close?version={file.Version}", null))
            .EnsureSuccessStatusCode();

        var dto = await c.GetFromJsonAsync<BillingContextController.BillingContextDto>(
            $"/api/v1/clients/{clientId}/branches/{br.Id}/billing-context");
        dto!.ActiveServiceFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task Primary_Branch_Alias_Falls_Through_To_First_Primary()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var brResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId));
        brResp.EnsureSuccessStatusCode();

        var dto = await c.GetFromJsonAsync<BillingContextController.BillingContextDto>(
            $"/api/v1/clients/{clientId}/billing-context");
        dto!.BranchIsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task Billing_Context_For_Client_Without_Primary_Branch_Returns_404()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, _, _) = await ServiceFilesTestFixtures.SeedClientWithServiceAsync(c, withBranch: false);
        var resp = await c.GetAsync($"/api/v1/clients/{clientId}/billing-context");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
