using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class BackfillTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public BackfillTests(CmsApiFactory f) => _factory = f;

    private static async Task<ServiceDto> GetOrCreateServiceAsync(HttpClient c, string code)
    {
        var create = await c.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto(code, code, null, null, 0, RequiredFieldsJson: "[]"));
        if (create.StatusCode == HttpStatusCode.Conflict)
        {
            var list = await c.GetFromJsonAsync<ServiceDto[]>($"/api/v1/masters/services?search={code}");
            return list!.First(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<ServiceDto>())!;
    }

    [Fact]
    public async Task Backfill_Links_State_Scoped_Files_To_Primary_Branch_And_Stamps_Gstin()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);

        // Give the client a primary branch with an active GSTIN + TAN.
        var brResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId));
        var br = (await brResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        (await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{br.Id}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Gstin, "29ABCDE1234F1Z5", null, null)))
            .EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{br.Id}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Tan, "BLRA12345B", null, null)))
            .EnsureSuccessStatusCode();

        // The backfill controller matches on the literal service Code "GST" (one of its StateScopedCodes).
        // Test factories use SQLite in-memory with no pre-seeded masters, so we create the "GST"
        // service directly. If a prior test already created it we reuse via search.
        var gst = await GetOrCreateServiceAsync(c, "GST");

        // Create a service file with NO branch (the state we're backfilling from).
        var fileResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                gst.Id, null, new DateOnly(2024, 4, 1), null, null,
                MetadataJson: null));
        fileResp.EnsureSuccessStatusCode();
        var file = (await fileResp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        file.BranchId.Should().BeNull();
        file.Gstin.Should().BeNull();

        // Dry run first — should report what WOULD change, not persist.
        var dry = await c.PostAsync("/api/v1/backfill/service-file-registrations?dryRun=true", null);
        dry.EnsureSuccessStatusCode();
        var dryBody = (await dry.Content.ReadFromJsonAsync<BackfillController.BackfillResult>())!;
        dryBody.DryRun.Should().BeTrue();
        dryBody.FilesLinkedToBranch.Should().BeGreaterThanOrEqualTo(1);

        var afterDry = await c.GetFromJsonAsync<ClientServiceFilesController.ServiceFileDto>(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}");
        afterDry!.BranchId.Should().BeNull(); // dry run did NOT persist

        // Real run.
        var real = await c.PostAsync("/api/v1/backfill/service-file-registrations", null);
        real.EnsureSuccessStatusCode();
        var realBody = (await real.Content.ReadFromJsonAsync<BackfillController.BackfillResult>())!;
        realBody.FilesLinkedToBranch.Should().BeGreaterThanOrEqualTo(1);

        var fileAfter = await c.GetFromJsonAsync<ClientServiceFilesController.ServiceFileDto>(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}");
        fileAfter!.BranchId.Should().Be(br.Id);
        fileAfter.Gstin.Should().Be("29ABCDE1234F1Z5");
        fileAfter.MetadataJson.Should().Contain("29ABCDE1234F1Z5");
    }

    [Fact]
    public async Task Backfill_Does_Not_Overwrite_Existing_Metadata()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var brResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId));
        var br = (await brResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        (await c.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/branches/{br.Id}/tax-registrations",
            new BranchTaxRegistrationsController.UpsertRequest(
                TaxRegistrationType.Gstin, "27AAAAA1111A1Z5", null, null)))
            .EnsureSuccessStatusCode();

        var gst = await GetOrCreateServiceAsync(c, "GST");

        // Create file WITH existing non-null metadata.
        var fileResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                gst.Id, null, new DateOnly(2024, 4, 1), null, null,
                MetadataJson: "{\"gstin\":\"EXISTING-GSTIN\",\"periodicity\":\"Monthly\"}"));
        fileResp.EnsureSuccessStatusCode();
        var file = (await fileResp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        var run = await c.PostAsync("/api/v1/backfill/service-file-registrations", null);
        run.EnsureSuccessStatusCode();

        var after = await c.GetFromJsonAsync<ClientServiceFilesController.ServiceFileDto>(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}");
        after!.MetadataJson.Should().Contain("EXISTING-GSTIN");
        after.MetadataJson.Should().NotContain("27AAAAA1111A1Z5");
        after.BranchId.Should().Be(br.Id); // still linked
    }

    [Fact]
    public async Task Backfill_Requires_Imports_Write_Permission()
    {
        var token = await _factory.CreateUserAndLoginAsync("backfill-denied@cms.local", "StaffUser");
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await c.PostAsync("/api/v1/backfill/service-file-registrations", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
