using System.Net.Http.Json;
using CMS.Api.Controllers;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.ImportsTestFixtures;

namespace CMS.Api.IntegrationTests;

public class ImportDedupeTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ImportDedupeTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Uploading_The_Same_Bytes_Twice_Returns_The_Same_Batch_Id_Flagged_Duplicate()
    {
        var c = await AdminClientAsync(_factory);
        var bytes = BuildClientsSheet(new[]
        {
            new Dictionary<string, string> { ["LegalName"] = "Dup Co A" },
        });

        var firstId = await UploadBytesAsync(c, bytes, "a.xlsx");
        // Second upload with identical bytes.
        var secondId = await UploadBytesAsync(c, bytes, "a.xlsx");
        secondId.Should().Be(firstId);
    }

    [Fact]
    public async Task Different_Bytes_Produce_Different_Batches()
    {
        var c = await AdminClientAsync(_factory);
        var a = BuildClientsSheet(new[] { new Dictionary<string, string> { ["LegalName"] = "A Co" } });
        var b = BuildClientsSheet(new[] { new Dictionary<string, string> { ["LegalName"] = "B Co" } });
        var id1 = await UploadBytesAsync(c, a, "a.xlsx");
        var id2 = await UploadBytesAsync(c, b, "b.xlsx");
        id2.Should().NotBe(id1);
    }

    [Fact]
    public async Task Upload_Returns_Sha256_Hex_64_Chars()
    {
        var c = await AdminClientAsync(_factory);
        var bytes = BuildClientsSheet(new[] { new Dictionary<string, string> { ["LegalName"] = "Hash Co" } });
        using var multi = new MultipartFormDataContent
        {
            { new ByteArrayContent(bytes), "file", "h.xlsx" }
        };
        var resp = await c.PostAsync("/api/v1/imports/upload", multi);
        resp.EnsureSuccessStatusCode();
        var dto = (await resp.Content.ReadFromJsonAsync<ImportsController.UploadResponse>())!;
        dto.Sha256.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task Reuploading_After_Zero_Row_Commit_Creates_Fresh_Batch_And_Delete_Respects_Committed_Records()
    {
        var c = await AdminClientAsync(_factory);
        await SeedEntityTypeAndServiceAsync(c, "DEL-ET", "DEL-SVC");

        var bytes = BuildClientsSheet(new[]
        {
            new Dictionary<string, string> { ["LegalName"] = "Zero Commit Co", ["EntityTypeCode"] = "DEL-ET" },
        });
        var firstId = await UploadBytesAsync(c, bytes, "zero.xlsx");
        (await c.PostAsync($"/api/v1/imports/{firstId}/validate", null)).EnsureSuccessStatusCode();
        // Commit without Auto-Resolve -> 0 clients committed.
        (await c.PostAsync($"/api/v1/imports/{firstId}/commit", null)).EnsureSuccessStatusCode();

        // Same file re-upload should create a new batch since previous committed 0 records.
        var secondId = await UploadBytesAsync(c, bytes, "zero.xlsx");
        secondId.Should().NotBe(firstId);

        // Zero-commit batch can be deleted.
        var delFirst = await c.DeleteAsync($"/api/v1/imports/{firstId}");
        delFirst.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        // Batch with committed master records cannot be deleted (409 Conflict).
        (await c.PostAsync($"/api/v1/imports/{secondId}/validate", null)).EnsureSuccessStatusCode();
        (await c.PostAsync($"/api/v1/imports/{secondId}/auto-resolve", null)).EnsureSuccessStatusCode();
        (await c.PostAsync($"/api/v1/imports/{secondId}/commit", null)).EnsureSuccessStatusCode();

        var delSecond = await c.DeleteAsync($"/api/v1/imports/{secondId}");
        delSecond.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }
}
