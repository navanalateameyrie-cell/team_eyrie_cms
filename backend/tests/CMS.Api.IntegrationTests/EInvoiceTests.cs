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

public class EInvoiceTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public EInvoiceTests(CmsApiFactory f) => _factory = f;

    private static async Task<InvoicesController.InvoiceDto> CreateInvoiceAsync(HttpClient c)
    {
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var br = (await (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId, isPrimary: true))).Content
            .ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        var svcCode = "SVC" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var svc = (await (await c.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto(svcCode, svcCode, null, null, 0, "[]", null))).Content
            .ReadFromJsonAsync<ServiceDto>())!;
        var create = await c.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoicesController.CreateInvoiceRequest(clientId, br.Id, stateId,
                new DateOnly(2024, 5, 10), new DateOnly(2024, 6, 10), null,
                new() { new InvoicesController.LineItemRequest("audit",
                    svc.Id, null, null, 1m, 1500m, 18m) }));
        return (await create.Content.ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
    }

    [Fact]
    public async Task Generate_Sets_Irn_And_Marks_Status()
    {
        var c = await AdminClientAsync(_factory);
        var inv = await CreateInvoiceAsync(c);
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/issue", null)).EnsureSuccessStatusCode();

        var gen = await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/einvoice/generate", null);
        gen.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await gen.Content.ReadFromJsonAsync<InvoicesController.GenerateIrnResponse>())!;
        body.Status.Should().Be(EInvoiceStatus.Generated);
        body.Irn.Should().NotBeNullOrEmpty().And.HaveLength(64);
        body.AckNo.Should().NotBeNullOrEmpty();
        body.AckDateUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Double_Generate_Returns_422()
    {
        var c = await AdminClientAsync(_factory);
        var inv = await CreateInvoiceAsync(c);
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/issue", null)).EnsureSuccessStatusCode();
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/einvoice/generate", null)).EnsureSuccessStatusCode();

        var again = await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/einvoice/generate", null);
        again.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Cancel_Transitions_Status_And_Second_Cancel_Fails()
    {
        var c = await AdminClientAsync(_factory);
        var inv = await CreateInvoiceAsync(c);
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/issue", null)).EnsureSuccessStatusCode();
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/einvoice/generate", null)).EnsureSuccessStatusCode();

        var cancel = await c.PostAsJsonAsync($"/api/v1/billing/invoices/{inv.Id}/einvoice/cancel",
            new InvoicesController.CancelIrnRequest("4"));
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await cancel.Content.ReadFromJsonAsync<InvoicesController.GenerateIrnResponse>())!;
        body.Status.Should().Be(EInvoiceStatus.Cancelled);

        var again = await c.PostAsJsonAsync($"/api/v1/billing/invoices/{inv.Id}/einvoice/cancel",
            new InvoicesController.CancelIrnRequest("duplicate"));
        again.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Pdf_Embeds_Qr_When_Irn_Present()
    {
        var c = await AdminClientAsync(_factory);
        var inv = await CreateInvoiceAsync(c);
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/issue", null)).EnsureSuccessStatusCode();

        var pdfBefore = await (await c.GetAsync($"/api/v1/billing/invoices/{inv.Id}/pdf"))
            .Content.ReadAsByteArrayAsync();
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/einvoice/generate", null)).EnsureSuccessStatusCode();

        var pdfAfter = await (await c.GetAsync($"/api/v1/billing/invoices/{inv.Id}/pdf"))
            .Content.ReadAsByteArrayAsync();
        pdfAfter.Should().NotEqual(pdfBefore,
            "the renderer should embed the QR image + IRN block once the invoice has an IRN");
        pdfAfter.Length.Should().BeGreaterThan(pdfBefore.Length);
    }

    [Fact]
    public async Task Einvoice_Requires_Billing_Write()
    {
        var token = await _factory.CreateUserAndLoginAsync("einv-nobody@cms.local", "StaffUser");
        var staff = _factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var admin = await AdminClientAsync(_factory);
        var inv = await CreateInvoiceAsync(admin);
        var r = await staff.PostAsync($"/api/v1/billing/invoices/{inv.Id}/einvoice/generate", null);
        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
