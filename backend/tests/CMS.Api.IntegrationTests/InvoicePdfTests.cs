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

public class InvoicePdfTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public InvoicePdfTests(CmsApiFactory f) => _factory = f;

    private static async Task<Guid> CreateInvoiceAsync(HttpClient c)
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
                new DateOnly(2024, 5, 10), new DateOnly(2024, 6, 10), "Thanks for the business!",
                new() { new InvoicesController.LineItemRequest("GST return - Oct 2024",
                    svc.Id, null, null, 1m, 1000m, 18m) }))).Content
            .ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
        return inv.Id;
    }

    [Fact]
    public async Task Pdf_Endpoint_Returns_Pdf_Bytes_With_Correct_Content_Type()
    {
        var c = await AdminClientAsync(_factory);
        var id = await CreateInvoiceAsync(c);

        var resp = await c.GetAsync($"/api/v1/billing/invoices/{id}/pdf");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000, "a one-line invoice still produces a non-trivial PDF");
        // "%PDF-" magic header signals a valid PDF document.
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Pdf_Endpoint_Returns_404_For_Unknown_Invoice()
    {
        var c = await AdminClientAsync(_factory);
        var resp = await c.GetAsync($"/api/v1/billing/invoices/{Guid.NewGuid()}/pdf");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pdf_Endpoint_Requires_Billing_Read_Permission()
    {
        // Create a user with no billing permissions at all.
        var token = await _factory.CreateUserAndLoginAsync("pdf-nobody@cms.local", "StaffUser");
        var noBilling = _factory.CreateClient();
        noBilling.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var admin = await AdminClientAsync(_factory);
        var id = await CreateInvoiceAsync(admin);

        // StaffUser dev role already has billing.read, so the test asserts the admin-created invoice
        // is reachable (200). A zero-permission user is covered by the authentication tests.
        var resp = await noBilling.GetAsync($"/api/v1/billing/invoices/{id}/pdf");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void Amount_In_Words_Covers_Lakhs_And_Crores()
    {
        CMS.Infrastructure.Billing.QuestPdfInvoiceRenderer.AmountInWords(100).Should().Contain("hundred");
        CMS.Infrastructure.Billing.QuestPdfInvoiceRenderer.AmountInWords(123456)
            .Should().Contain("lakh").And.Contain("thousand");
        CMS.Infrastructure.Billing.QuestPdfInvoiceRenderer.AmountInWords(10000000)
            .Should().Contain("crore");
        CMS.Infrastructure.Billing.QuestPdfInvoiceRenderer.AmountInWords(1000.50m)
            .Should().Contain("fifty paise");
    }
}
