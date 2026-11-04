using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.ImportsTestFixtures;

namespace CMS.Api.IntegrationTests;

public class ImportParseTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ImportParseTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Upload_Parses_Rows_Into_Staging_Table_And_Status_Is_Parsed()
    {
        var c = await AdminClientAsync(_factory);
        var bytes = BuildClientsSheet(new[]
        {
            new Dictionary<string, string> { ["LegalName"] = "Alpha Ltd", ["PAN"] = "ABCDE1234F" },
            new Dictionary<string, string> { ["LegalName"] = "Bravo Pvt", ["PAN"] = "XYZAB9876C" },
        });
        var batchId = await UploadBytesAsync(c, bytes, "clients.xlsx");

        var batch = await c.GetFromJsonAsync<ImportsController.BatchDto>($"/api/v1/imports/{batchId}");
        batch!.Status.Should().Be(ImportBatchStatus.Parsed);
        batch.RowCount.Should().Be(2);
        batch.SourceSheet.Should().Be("Clients");

        var rows = await c.GetFromJsonAsync<ImportsController.StagingClientDto[]>($"/api/v1/imports/{batchId}/rows");
        rows!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Final_Sheet_Service_Columns_Produce_Service_File_Staging_Rows()
    {
        var c = await AdminClientAsync(_factory);
        var bytes = BuildFinalSheet(
            serviceCodes: new[] { "GST", "IT", "TDS" },
            rows: new[]
            {
                new Dictionary<string, string>
                {
                    ["LegalName"] = "Multi Co Pvt Ltd",
                    ["PAN"] = "ABCDE1234F",
                    ["Service_GST"] = "LEG-GST-001",
                    ["Service_IT"]  = "Y",
                    // TDS blank → no staging row
                },
                new Dictionary<string, string>
                {
                    ["LegalName"] = "Single Co",
                    ["Service_GST"] = "Y",
                },
            });

        var batchId = await UploadBytesAsync(c, bytes, "final.xlsx");

        var sfRows = await c.GetFromJsonAsync<ImportsController.StagingServiceFileDto[]>(
            $"/api/v1/imports/{batchId}/service-file-rows");
        sfRows!.Should().HaveCount(3); // 2 for row-1 + 1 for row-2
        sfRows.Count(r => r.ServiceCode == "GST").Should().Be(2);
        sfRows.Count(r => r.ServiceCode == "IT").Should().Be(1);
        // Marker values collapse to null LegacyFileNumber; explicit values are preserved.
        sfRows.First(r => r.ServiceCode == "GST" && r.RowNumber == 2).LegacyFileNumber.Should().Be("LEG-GST-001");
        sfRows.First(r => r.ServiceCode == "IT").LegacyFileNumber.Should().BeNull();
    }

    [Fact]
    public async Task Blank_Rows_Are_Skipped()
    {
        var c = await AdminClientAsync(_factory);
        var bytes = BuildClientsSheet(new[]
        {
            new Dictionary<string, string> { ["LegalName"] = "Row 1" },
            new Dictionary<string, string>(), // all blank
            new Dictionary<string, string> { ["LegalName"] = "Row 3" },
        });
        var batchId = await UploadBytesAsync(c, bytes, "with-blank.xlsx");

        var batch = await c.GetFromJsonAsync<ImportsController.BatchDto>($"/api/v1/imports/{batchId}");
        batch!.RowCount.Should().Be(2);
    }
}
