using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.ImportsTestFixtures;

namespace CMS.Api.IntegrationTests;

public class ImportMatchingAndCommitTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ImportMatchingAndCommitTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Validation_Flags_Bad_Pan_And_Unknown_Entity_Type()
    {
        var c = await AdminClientAsync(_factory);
        await SeedEntityTypeAndServiceAsync(c, "IND", "GST");

        var bytes = BuildClientsSheet(new[]
        {
            new Dictionary<string, string> { ["LegalName"] = "Good", ["PAN"] = "ABCDE1234F", ["EntityTypeCode"] = "IND" },
            new Dictionary<string, string> { ["LegalName"] = "Bad PAN", ["PAN"] = "NOTVALID", ["EntityTypeCode"] = "IND" },
            new Dictionary<string, string> { ["LegalName"] = "Unknown ET", ["EntityTypeCode"] = "NOPE" },
        });
        var batchId = await UploadBytesAsync(c, bytes, "val.xlsx");
        (await c.PostAsync($"/api/v1/imports/{batchId}/validate", null)).EnsureSuccessStatusCode();

        var rows = (await c.GetFromJsonAsync<ImportsController.StagingClientDto[]>(
            $"/api/v1/imports/{batchId}/rows"))!;
        rows.Should().HaveCount(3);

        var badPan = rows.Single(r => r.NormalizedLegalName == "Bad PAN");
        badPan.ValidationErrorsJson.Should().Contain("invalid_pan_format");

        var unknownEt = rows.Single(r => r.NormalizedLegalName == "Unknown ET");
        unknownEt.ValidationErrorsJson.Should().Contain("unknown_entity_type");

        var batch = await c.GetFromJsonAsync<ImportsController.BatchDto>($"/api/v1/imports/{batchId}");
        batch!.Status.Should().Be(ImportBatchStatus.Validated);
        batch.ErrorCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task AutoResolve_Creates_For_No_Candidate_Updates_For_Single_Pan_Match()
    {
        var c = await AdminClientAsync(_factory);
        await SeedEntityTypeAndServiceAsync(c, "AR-ET", "AR-SVC");

        // Pre-create a client that will become the "single PAN candidate".
        var etDto = await c.GetFromJsonAsync<EntityTypeDto[]>("/api/v1/masters/entity-types?search=AR-ET");
        var etId = etDto!.First(x => x.Code == "AR-ET").Id;
        var existingPan = $"QWERT{new Random().Next(1000, 9999)}Z";
        var createResp = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Existing Co", etId, null, existingPan, null, null));
        createResp.EnsureSuccessStatusCode();

        var bytes = BuildClientsSheet(new[]
        {
            new Dictionary<string, string> { ["LegalName"] = "Brand New Co", ["EntityTypeCode"] = "AR-ET" },
            new Dictionary<string, string> { ["LegalName"] = "Existing Renamed", ["PAN"] = existingPan, ["EntityTypeCode"] = "AR-ET" },
        });
        var batchId = await UploadBytesAsync(c, bytes, "ar.xlsx");
        (await c.PostAsync($"/api/v1/imports/{batchId}/validate", null)).EnsureSuccessStatusCode();
        (await c.PostAsync($"/api/v1/imports/{batchId}/auto-resolve", null)).EnsureSuccessStatusCode();

        var rows = (await c.GetFromJsonAsync<ImportsController.StagingClientDto[]>(
            $"/api/v1/imports/{batchId}/rows"))!;
        rows.Single(r => r.NormalizedLegalName == "Brand New Co").Decision.Should().Be(ImportDecision.CreateNew);
        var updateRow = rows.Single(r => r.NormalizedLegalName == "Existing Renamed");
        updateRow.Decision.Should().Be(ImportDecision.UpdateExisting);
        updateRow.DecidedClientId.Should().NotBeNull();
    }

    [Fact]
    public async Task Commit_Creates_New_Client_Through_Staging_With_Permanent_Code()
    {
        var c = await AdminClientAsync(_factory);
        await SeedEntityTypeAndServiceAsync(c, "C-ET", "C-SVC");

        var bytes = BuildClientsSheet(new[]
        {
            new Dictionary<string, string> { ["LegalName"] = "Phase 11 Client", ["PAN"] = "PQRST1234U", ["EntityTypeCode"] = "C-ET" },
        });
        var batchId = await UploadBytesAsync(c, bytes, "commit.xlsx");
        (await c.PostAsync($"/api/v1/imports/{batchId}/validate", null)).EnsureSuccessStatusCode();
        (await c.PostAsync($"/api/v1/imports/{batchId}/auto-resolve", null)).EnsureSuccessStatusCode();

        var commitResp = await c.PostAsync($"/api/v1/imports/{batchId}/commit", null);
        commitResp.EnsureSuccessStatusCode();
        var commit = (await commitResp.Content.ReadFromJsonAsync<ImportsController.CommitDto>())!;
        commit.ClientsCreated.Should().Be(1);

        var rows = (await c.GetFromJsonAsync<ImportsController.StagingClientDto[]>(
            $"/api/v1/imports/{batchId}/rows"))!;
        var committedId = rows.Single().CommittedClientId!.Value;
        var client = await c.GetFromJsonAsync<ClientsController.ClientDto>($"/api/v1/clients/{committedId}");
        client!.Code.Should().MatchRegex("^[0-9]{8}$");
        client.LegalName.Should().Be("Phase 11 Client");
        client.Pan.Should().Be("PQRST1234U");
    }

    [Fact]
    public async Task Final_Sheet_Commit_Creates_Service_Files_For_Each_Service_Column()
    {
        var c = await AdminClientAsync(_factory);
        await SeedEntityTypeAndServiceAsync(c, "F-ET", "GST");
        await SeedEntityTypeAndServiceAsync(c, "F-ET", "IT");

        var bytes = BuildFinalSheet(
            serviceCodes: new[] { "GST", "IT" },
            rows: new[]
            {
                new Dictionary<string, string>
                {
                    ["LegalName"] = "Final Sheet Co",
                    ["PAN"] = "FINAL1234A",
                    ["Service_GST"] = "LEG-GST-77",
                    ["Service_IT"]  = "Y",
                },
            });
        var batchId = await UploadBytesAsync(c, bytes, "final.xlsx");
        (await c.PostAsync($"/api/v1/imports/{batchId}/validate", null)).EnsureSuccessStatusCode();
        (await c.PostAsync($"/api/v1/imports/{batchId}/auto-resolve", null)).EnsureSuccessStatusCode();

        var commit = (await (await c.PostAsync($"/api/v1/imports/{batchId}/commit", null))
            .Content.ReadFromJsonAsync<ImportsController.CommitDto>())!;
        commit.ClientsCreated.Should().Be(1);
        commit.ServiceFilesCreated.Should().Be(2);

        var rows = (await c.GetFromJsonAsync<ImportsController.StagingClientDto[]>(
            $"/api/v1/imports/{batchId}/rows"))!;
        var clientId = rows.Single().CommittedClientId!.Value;
        var files = (await c.GetFromJsonAsync<ClientServiceFilesController.ServiceFileDto[]>(
            $"/api/v1/clients/{clientId}/service-files"))!;
        files.Should().HaveCount(2);
        files.Select(f => f.LegacyFileNumber).Should().Contain(new[] { "LEG-GST-77", (string?)null });
    }

    [Fact]
    public async Task Reconciliation_Reports_Final_Decision_Mix()
    {
        var c = await AdminClientAsync(_factory);
        await SeedEntityTypeAndServiceAsync(c, "R-ET", "R-SVC");

        var bytes = BuildClientsSheet(new[]
        {
            new Dictionary<string, string> { ["LegalName"] = "A", ["EntityTypeCode"] = "R-ET" },
            new Dictionary<string, string> { ["LegalName"] = "B", ["EntityTypeCode"] = "R-ET" },
            new Dictionary<string, string> { ["LegalName"] = "Bad", ["PAN"] = "oops" }, // invalid PAN format
        });
        var batchId = await UploadBytesAsync(c, bytes, "reco.xlsx");
        (await c.PostAsync($"/api/v1/imports/{batchId}/validate", null)).EnsureSuccessStatusCode();
        (await c.PostAsync($"/api/v1/imports/{batchId}/auto-resolve", null)).EnsureSuccessStatusCode();

        var reco = await c.GetFromJsonAsync<ImportsController.ReconciliationDto>(
            $"/api/v1/imports/{batchId}/reconciliation");
        reco!.CreateNew.Should().Be(2);
        reco.Pending.Should().Be(1); // the invalid-PAN row stays Pending (errors block auto-resolve)
        reco.ClientRowsWithErrors.Should().Be(1);
    }

    [Fact]
    public async Task Commit_From_Not_Validated_Batch_Returns_500_Problem()
    {
        // Our engine throws if commit is called out-of-order; the global exception middleware
        // surfaces it as a problem+json. We only assert it is NOT 200.
        var c = await AdminClientAsync(_factory);
        await SeedEntityTypeAndServiceAsync(c, "N-ET", "N-SVC");
        var bytes = BuildClientsSheet(new[]
        {
            new Dictionary<string, string> { ["LegalName"] = "Early" },
        });
        var batchId = await UploadBytesAsync(c, bytes, "early.xlsx");
        // Skip validate — go straight to commit.
        var resp = await c.PostAsync($"/api/v1/imports/{batchId}/commit", null);
        resp.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Only_SystemAdmin_And_Manager_Can_Read_Imports()
    {
        var token = await _factory.CreateUserAndLoginAsync("imports-reader@cms.local", "Manager");
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        (await c.GetAsync("/api/v1/imports")).StatusCode.Should().Be(HttpStatusCode.OK);
        // Manager has no imports.write — upload should be forbidden.
        using var multi = new MultipartFormDataContent
        {
            { new ByteArrayContent(new byte[] { 1, 2, 3 }), "file", "x.xlsx" }
        };
        var upload = await c.PostAsync("/api/v1/imports/upload", multi);
        upload.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
