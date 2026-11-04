using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class DocumentsTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public DocumentsTests(CmsApiFactory f) => _factory = f;

    private static MultipartFormDataContent MakeUpload(byte[] bytes, string filename, string contentType,
        DocumentOwnerType ownerType, Guid ownerId, string? category = null, string? description = null)
    {
        var mp = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        mp.Add(fileContent, "File", filename);
        mp.Add(new StringContent(((int)ownerType).ToString()), "OwnerType");
        mp.Add(new StringContent(ownerId.ToString()), "OwnerId");
        if (category is not null) mp.Add(new StringContent(category), "Category");
        if (description is not null) mp.Add(new StringContent(description), "Description");
        return mp;
    }

    [Fact]
    public async Task Upload_Then_List_Download_Succeeds()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, _) = await SeedClientAndMastersAsync(c);

        var bytes = Encoding.UTF8.GetBytes("Hello, PDF-ish bytes for test");
        var uploadResp = await c.PostAsync("/api/v1/documents/upload",
            MakeUpload(bytes, "notice.pdf", "application/pdf",
                DocumentOwnerType.Client, clientId, "notice", "An IT notice"));
        uploadResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = (await uploadResp.Content.ReadFromJsonAsync<DocumentsController.DocumentDto>())!;
        doc.OriginalFileName.Should().Be("notice.pdf");
        doc.SizeBytes.Should().Be(bytes.Length);
        doc.Sha256.Should().HaveLength(64);
        doc.Owners.Should().ContainSingle();

        var list = await c.GetFromJsonAsync<DocumentsController.DocumentDto[]>(
            $"/api/v1/documents?ownerType={(int)DocumentOwnerType.Client}&ownerId={clientId}");
        list!.Should().ContainSingle(d => d.Id == doc.Id);

        var download = await c.GetAsync($"/api/v1/documents/{doc.Id}/download");
        download.EnsureSuccessStatusCode();
        var downloaded = await download.Content.ReadAsByteArrayAsync();
        downloaded.Should().Equal(bytes);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Duplicate_Bytes_Dedupe_To_Same_StorageKey_But_Separate_Rows()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, _) = await SeedClientAndMastersAsync(c);

        var bytes = Encoding.UTF8.GetBytes("identical payload");
        var r1 = await c.PostAsync("/api/v1/documents/upload",
            MakeUpload(bytes, "a.pdf", "application/pdf", DocumentOwnerType.Client, clientId));
        var r2 = await c.PostAsync("/api/v1/documents/upload",
            MakeUpload(bytes, "b.pdf", "application/pdf", DocumentOwnerType.Client, clientId));
        r1.EnsureSuccessStatusCode(); r2.EnsureSuccessStatusCode();
        var d1 = (await r1.Content.ReadFromJsonAsync<DocumentsController.DocumentDto>())!;
        var d2 = (await r2.Content.ReadFromJsonAsync<DocumentsController.DocumentDto>())!;
        d1.Id.Should().NotBe(d2.Id, "each upload becomes its own catalog row");
        d1.Sha256.Should().Be(d2.Sha256, "same bytes → same hash → dedupe on disk");
    }

    [Fact]
    public async Task Unsupported_Content_Type_Is_Rejected()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, _) = await SeedClientAndMastersAsync(c);
        var r = await c.PostAsync("/api/v1/documents/upload",
            MakeUpload(new byte[] { 1, 2, 3 }, "x.exe", "application/x-dosexec",
                DocumentOwnerType.Client, clientId));
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Unknown_Owner_Is_Rejected()
    {
        var c = await AdminClientAsync(_factory);
        var r = await c.PostAsync("/api/v1/documents/upload",
            MakeUpload(new byte[] { 1 }, "a.pdf", "application/pdf",
                DocumentOwnerType.Client, Guid.NewGuid()));
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Soft_Delete_Hides_From_List_And_Returns_404_On_Get()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, _) = await SeedClientAndMastersAsync(c);
        var r = await c.PostAsync("/api/v1/documents/upload",
            MakeUpload(Encoding.UTF8.GetBytes("tbd"), "tbd.pdf", "application/pdf",
                DocumentOwnerType.Client, clientId));
        r.EnsureSuccessStatusCode();
        var doc = (await r.Content.ReadFromJsonAsync<DocumentsController.DocumentDto>())!;

        var del = await c.DeleteAsync($"/api/v1/documents/{doc.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await c.GetAsync($"/api/v1/documents/{doc.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await c.GetFromJsonAsync<DocumentsController.DocumentDto[]>(
            $"/api/v1/documents?ownerType={(int)DocumentOwnerType.Client}&ownerId={clientId}");
        list!.Should().NotContain(d => d.Id == doc.Id);
    }

    [Fact]
    public async Task Detach_Removes_Link_But_Keeps_Document()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, _) = await SeedClientAndMastersAsync(c);
        var r = await c.PostAsync("/api/v1/documents/upload",
            MakeUpload(Encoding.UTF8.GetBytes("multi-owner"), "shared.pdf", "application/pdf",
                DocumentOwnerType.Client, clientId));
        var doc = (await r.Content.ReadFromJsonAsync<DocumentsController.DocumentDto>())!;

        var detach = await c.DeleteAsync(
            $"/api/v1/documents/{doc.Id}/owners?ownerType={(int)DocumentOwnerType.Client}&ownerId={clientId}");
        detach.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var still = await c.GetFromJsonAsync<DocumentsController.DocumentDto>($"/api/v1/documents/{doc.Id}");
        still!.Owners.Should().BeEmpty("the catalog row survives, just orphaned");
    }

    [Fact]
    public async Task Delete_Requires_Delete_Permission()
    {
        var staffToken = await _factory.CreateUserAndLoginAsync("docs-staff@cms.local", "StaffUser");
        var staff = _factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        // StaffUser has documents.read + write but NOT delete per the dev seed.
        var admin = await AdminClientAsync(_factory);
        var (clientId, _, _) = await SeedClientAndMastersAsync(admin);
        var up = await admin.PostAsync("/api/v1/documents/upload",
            MakeUpload(Encoding.UTF8.GetBytes("locked"), "x.pdf", "application/pdf",
                DocumentOwnerType.Client, clientId));
        var doc = (await up.Content.ReadFromJsonAsync<DocumentsController.DocumentDto>())!;

        var denied = await staff.DeleteAsync($"/api/v1/documents/{doc.Id}");
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
