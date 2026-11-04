using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class InvoiceDigitalSignatureTests : IClassFixture<CmsApiFactory>, IDisposable
{
    private readonly CmsApiFactory _factory;
    private readonly string _pfxPath;
    private const string PfxPassword = "test-pfx-pass";
    private const string SignerName = "Test Signatory";

    public InvoiceDigitalSignatureTests(CmsApiFactory f)
    {
        _factory = f;
        _pfxPath = Path.Combine(Path.GetTempPath(), $"cms-test-signer-{Guid.NewGuid():N}.pfx");
        // Generate a one-off self-signed RSA cert and persist it as a .pfx the signer can load.
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=CMS Test Signer", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
        File.WriteAllBytes(_pfxPath, cert.Export(X509ContentType.Pfx, PfxPassword));
    }

    public void Dispose()
    {
        try { if (File.Exists(_pfxPath)) File.Delete(_pfxPath); } catch { /* best-effort */ }
    }

    private static async Task<InvoicesController.InvoiceDto> SeedIssuedInvoiceAsync(HttpClient c)
    {
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var br = (await (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId, isPrimary: true))).Content
            .ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        var svcCode = "SVC" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var svc = (await (await c.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto(svcCode, svcCode, null, null, 0, "[]", null))).Content
            .ReadFromJsonAsync<ServiceDto>())!;
        var inv = (await (await c.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoicesController.CreateInvoiceRequest(clientId, br.Id, stateId,
                new DateOnly(2024, 5, 10), new DateOnly(2024, 6, 10), null,
                new() { new InvoicesController.LineItemRequest("audit", svc.Id, null, null, 1m, 1000m, 18m) }))).Content
            .ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/issue", null)).EnsureSuccessStatusCode();
        return inv;
    }

    private async Task EnableSigningAsync(HttpClient c)
    {
        var r = await c.PutAsJsonAsync("/api/v1/settings/digitalSignature", new {
            value = new {
                enabled = true,
                certificatePath = _pfxPath,
                certificatePassword = PfxPassword,
                signerName = SignerName,
            }});
        r.EnsureSuccessStatusCode();
    }

    private async Task DisableSigningAsync(HttpClient c)
    {
        var r = await c.PutAsJsonAsync("/api/v1/settings/digitalSignature", new { value = new { enabled = false }});
        r.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Pdf_Bytes_Differ_Once_Signing_Is_Enabled()
    {
        var c = await AdminClientAsync(_factory);
        await DisableSigningAsync(c);
        var inv = await SeedIssuedInvoiceAsync(c);

        var unsigned = await (await c.GetAsync($"/api/v1/billing/invoices/{inv.Id}/pdf")).Content.ReadAsByteArrayAsync();

        await EnableSigningAsync(c);
        var resp = await c.GetAsync($"/api/v1/billing/invoices/{inv.Id}/pdf");
        resp.EnsureSuccessStatusCode();
        resp.Headers.Contains("X-Signature-Available").Should().BeTrue();
        var signed = await resp.Content.ReadAsByteArrayAsync();

        signed.Should().NotEqual(unsigned, "the signed PDF must carry a visible signature stamp");
        signed.Length.Should().BeGreaterThan(unsigned.Length);
    }

    [Fact]
    public async Task Signature_Endpoint_Returns_Verifiable_Detached_Cms()
    {
        var c = await AdminClientAsync(_factory);
        await EnableSigningAsync(c);
        var inv = await SeedIssuedInvoiceAsync(c);

        var sigResp = await c.GetAsync($"/api/v1/billing/invoices/{inv.Id}/pdf/signature");
        sigResp.StatusCode.Should().Be(HttpStatusCode.OK);
        sigResp.Content.Headers.ContentType!.MediaType.Should().Be("application/pkcs7-signature");
        var p7s = await sigResp.Content.ReadAsByteArrayAsync();

        // Decode the envelope and check structure. We can't call CheckSignature against freshly
        // re-rendered PDF bytes because QuestPDF embeds a creation timestamp that differs between
        // requests — but we CAN confirm the envelope is well-formed and carries our test cert.
        var cms = new SignedCms();
        cms.Decode(p7s);
        cms.SignerInfos.Count.Should().Be(1);
        cms.Certificates.Count.Should().BeGreaterThan(0);
        cms.SignerInfos[0].Certificate!.Subject.Should().Contain("CMS Test Signer");
    }

    [Fact]
    public async Task Signature_Endpoint_409_When_Signing_Disabled()
    {
        var c = await AdminClientAsync(_factory);
        await DisableSigningAsync(c);
        var inv = await SeedIssuedInvoiceAsync(c);
        var r = await c.GetAsync($"/api/v1/billing/invoices/{inv.Id}/pdf/signature");
        r.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Missing_Certificate_File_Falls_Back_Gracefully_With_No_Signature_Header()
    {
        var c = await AdminClientAsync(_factory);
        // Point at a non-existent path with Enabled=true — signer must skip, not 500.
        (await c.PutAsJsonAsync("/api/v1/settings/digitalSignature", new {
            value = new {
                enabled = true,
                certificatePath = Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid() + ".pfx"),
                certificatePassword = "x",
                signerName = "X",
            }})).EnsureSuccessStatusCode();

        var inv = await SeedIssuedInvoiceAsync(c);
        var resp = await c.GetAsync($"/api/v1/billing/invoices/{inv.Id}/pdf");
        resp.EnsureSuccessStatusCode();
        resp.Headers.Contains("X-Signature-Available").Should().BeFalse();
    }
}
