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

public class DocumentSignedUrlTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public DocumentSignedUrlTests(CmsApiFactory f) => _factory = f;

    private static MultipartFormDataContent MakeUpload(byte[] bytes, string filename, string contentType,
        DocumentOwnerType ownerType, Guid ownerId)
    {
        var mp = new MultipartFormDataContent();
        var fc = new ByteArrayContent(bytes);
        fc.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        mp.Add(fc, "File", filename);
        mp.Add(new StringContent(((int)ownerType).ToString()), "OwnerType");
        mp.Add(new StringContent(ownerId.ToString()), "OwnerId");
        return mp;
    }

    private async Task<(HttpClient admin, Guid docId, byte[] bytes)> UploadAsync()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, _) = await SeedClientAndMastersAsync(c);
        var bytes = Encoding.UTF8.GetBytes("signed-url-content");
        var up = await c.PostAsync("/api/v1/documents/upload",
            MakeUpload(bytes, "s.pdf", "application/pdf", DocumentOwnerType.Client, clientId));
        up.EnsureSuccessStatusCode();
        var doc = (await up.Content.ReadFromJsonAsync<DocumentsController.DocumentDto>())!;
        return (c, doc.Id, bytes);
    }

    [Fact]
    public async Task Signed_Url_Allows_Anonymous_Download()
    {
        var (admin, docId, bytes) = await UploadAsync();
        var resp = await admin.GetFromJsonAsync<DocumentsController.SignedUrlResponse>(
            $"/api/v1/documents/{docId}/signed-url?ttlSeconds=600");
        resp!.Url.Should().Contain("/api/v1/documents/signed/").And.Contain("sig=");

        var anon = _factory.CreateClient();           // no Authorization header
        var dl = await anon.GetAsync(resp.Url);
        dl.StatusCode.Should().Be(HttpStatusCode.OK);
        (await dl.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
    }

    [Fact]
    public async Task Tampered_Signature_Is_Rejected()
    {
        var (admin, docId, _) = await UploadAsync();
        var resp = await admin.GetFromJsonAsync<DocumentsController.SignedUrlResponse>(
            $"/api/v1/documents/{docId}/signed-url?ttlSeconds=600");
        // Flip two chars of the sig param.
        var tampered = resp!.Url.Replace("sig=", "sig=XX");
        var anon = _factory.CreateClient();
        var dl = await anon.GetAsync(tampered);
        dl.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Expired_Signature_Is_Rejected()
    {
        var (admin, docId, _) = await UploadAsync();
        var resp = await admin.GetFromJsonAsync<DocumentsController.SignedUrlResponse>(
            $"/api/v1/documents/{docId}/signed-url?ttlSeconds=30");
        // Replace exp with a very past timestamp — this also invalidates the sig, so the server's
        // short-circuit on exp is what we actually assert here.
        var expired = System.Text.RegularExpressions.Regex.Replace(resp!.Url, @"exp=\d+", "exp=1");
        var anon = _factory.CreateClient();
        var dl = await anon.GetAsync(expired);
        dl.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Version_Scoped_Signature_Does_Not_Serve_Current()
    {
        var (admin, docId, _) = await UploadAsync();

        // Create a v2 so there is an archive row for v1.
        var v2 = new MultipartFormDataContent();
        var fc = new ByteArrayContent(Encoding.UTF8.GetBytes("v2-bytes"));
        fc.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        v2.Add(fc, "File", "s.pdf");
        var up2 = await admin.PostAsync($"/api/v1/documents/{docId}/versions", v2);
        up2.EnsureSuccessStatusCode();
        var archived = (await up2.Content.ReadFromJsonAsync<DocumentsController.DocumentVersionDto>())!;

        // Sign specifically for v1 (archive).
        var signed = await admin.GetFromJsonAsync<DocumentsController.SignedUrlResponse>(
            $"/api/v1/documents/{docId}/signed-url?ttlSeconds=600&versionId={archived.Id}");
        var anon = _factory.CreateClient();
        var dl = await anon.GetAsync(signed!.Url);
        dl.EnsureSuccessStatusCode();
        (await dl.Content.ReadAsByteArrayAsync()).Should().Equal(Encoding.UTF8.GetBytes("signed-url-content"));

        // Stripping ?v= from the URL yields a sig that no longer matches (anonymous route sees versionId=null).
        var noVer = System.Text.RegularExpressions.Regex.Replace(signed.Url, @"&v=[0-9a-fA-F]+", "");
        var dl2 = await anon.GetAsync(noVer);
        dl2.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
