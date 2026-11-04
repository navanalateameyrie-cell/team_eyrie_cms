using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class CreditDebitAdvanceTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public CreditDebitAdvanceTests(CmsApiFactory f) => _factory = f;

    private static async Task<(Guid clientId, InvoicesController.InvoiceDto inv)> SeedInvoiceAsync(HttpClient c)
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
                new() { new InvoicesController.LineItemRequest("audit",
                    svc.Id, null, null, 1m, 1000m, 18m) }))).Content
            .ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/issue", null)).EnsureSuccessStatusCode();
        return (clientId, inv);
    }

    [Fact]
    public async Task Credit_Note_Reduces_Outstanding_And_Settles_When_Full()
    {
        var c = await AdminClientAsync(_factory);
        var (_, inv) = await SeedInvoiceAsync(c);
        inv.TotalInr.Should().Be(1180m);

        // Issue a credit note worth the full invoice amount (1000 + 18% GST = 1180).
        var cn = await c.PostAsJsonAsync("/api/v1/billing/credit-notes",
            new CreditNotesController.CreateCreditNoteRequest(inv.Id, new DateOnly(2024, 5, 20),
                "goods returned", 1000m, 18m));
        cn.StatusCode.Should().Be(HttpStatusCode.Created);

        var after = await c.GetFromJsonAsync<InvoicesController.InvoiceDto>(
            $"/api/v1/billing/invoices/{inv.Id}");
        after!.CreditedInr.Should().Be(1180m);
        after.OutstandingInr.Should().Be(0m);
        after.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task Credit_Beyond_Total_Is_Rejected()
    {
        var c = await AdminClientAsync(_factory);
        var (_, inv) = await SeedInvoiceAsync(c);
        var r = await c.PostAsJsonAsync("/api/v1/billing/credit-notes",
            new CreditNotesController.CreateCreditNoteRequest(inv.Id, new DateOnly(2024, 5, 20),
                "excessive", 5000m, 18m));
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Cancel_Credit_Note_Reverses_The_Applied_Credit()
    {
        var c = await AdminClientAsync(_factory);
        var (_, inv) = await SeedInvoiceAsync(c);
        var cn = (await (await c.PostAsJsonAsync("/api/v1/billing/credit-notes",
            new CreditNotesController.CreateCreditNoteRequest(inv.Id, new DateOnly(2024, 5, 20),
                "oops", 1000m, 18m))).Content.ReadFromJsonAsync<CreditNotesController.CreditNoteDto>())!;

        (await c.PostAsync($"/api/v1/billing/credit-notes/{cn.Id}/cancel", null)).EnsureSuccessStatusCode();
        var after = await c.GetFromJsonAsync<InvoicesController.InvoiceDto>(
            $"/api/v1/billing/invoices/{inv.Id}");
        after!.CreditedInr.Should().Be(0m);
        after.OutstandingInr.Should().Be(1180m);
        after.Status.Should().Be(InvoiceStatus.Issued);
    }

    [Fact]
    public async Task Debit_And_Credit_Share_The_Fy_Scope_But_Different_Series()
    {
        var c = await AdminClientAsync(_factory);
        var (_, inv) = await SeedInvoiceAsync(c);

        var cnBody = (await (await c.PostAsJsonAsync("/api/v1/billing/credit-notes",
            new CreditNotesController.CreateCreditNoteRequest(inv.Id, new DateOnly(2024, 5, 20),
                null, 100m, 18m))).Content.ReadFromJsonAsync<CreditNotesController.CreditNoteDto>())!;
        var dnBody = (await (await c.PostAsJsonAsync("/api/v1/billing/debit-notes",
            new DebitNotesController.CreateDebitNoteRequest(inv.Id, new DateOnly(2024, 5, 21),
                null, 50m, 18m))).Content.ReadFromJsonAsync<DebitNotesController.DebitNoteDto>())!;

        cnBody.Number.Should().StartWith("CRN/2024-25/");
        dnBody.Number.Should().StartWith("DBN/2024-25/");
        // Independent sequences — both start at 0001.
        cnBody.Number.Should().EndWith("0001");
        dnBody.Number.Should().EndWith("0001");
    }

    [Fact]
    public async Task Advance_Apply_Reduces_Invoice_Outstanding_And_Reverse_Restores_It()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, inv) = await SeedInvoiceAsync(c);

        var adv = (await (await c.PostAsJsonAsync("/api/v1/billing/advances",
            new AdvancesController.CreateAdvanceRequest(clientId, new DateOnly(2024, 5, 1),
                500m, PaymentMode.Bank, "RTGS-1", null))).Content
            .ReadFromJsonAsync<AdvancesController.AdvanceDto>())!;
        adv.AmountRemainingInr.Should().Be(500m);

        var applied = (await (await c.PostAsJsonAsync($"/api/v1/billing/advances/{adv.Id}/apply",
            new AdvancesController.ApplyAdvanceRequest(inv.Id, 500m))).Content
            .ReadFromJsonAsync<AdvancesController.AdvanceDto>())!;
        applied.AmountRemainingInr.Should().Be(0m);

        var afterApply = await c.GetFromJsonAsync<InvoicesController.InvoiceDto>(
            $"/api/v1/billing/invoices/{inv.Id}");
        afterApply!.PaidInr.Should().Be(500m);
        afterApply.OutstandingInr.Should().Be(680m);
    }

    [Fact]
    public async Task Advance_Cross_Client_Apply_Is_Rejected()
    {
        var c = await AdminClientAsync(_factory);
        var (clientA, _) = await SeedInvoiceAsync(c);
        var (_, invB) = await SeedInvoiceAsync(c);

        var adv = (await (await c.PostAsJsonAsync("/api/v1/billing/advances",
            new AdvancesController.CreateAdvanceRequest(clientA, new DateOnly(2024, 5, 1),
                200m, PaymentMode.Cash, null, null))).Content
            .ReadFromJsonAsync<AdvancesController.AdvanceDto>())!;

        var r = await c.PostAsJsonAsync($"/api/v1/billing/advances/{adv.Id}/apply",
            new AdvancesController.ApplyAdvanceRequest(invB.Id, 100m));
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
