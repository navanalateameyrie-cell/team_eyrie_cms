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

public class BillingTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public BillingTests(CmsApiFactory f) => _factory = f;

    private static async Task<(Guid clientId, Guid branchId, Guid stateId, Guid serviceId)> SetupClientBranchServiceAsync(HttpClient c)
    {
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var brResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId, isPrimary: true));
        brResp.EnsureSuccessStatusCode();
        var br = (await brResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        var svcCode = "SVC" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var svcResp = await c.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto(svcCode, svcCode, null, null, 0, "[]", null));
        svcResp.EnsureSuccessStatusCode();
        var svc = (await svcResp.Content.ReadFromJsonAsync<ServiceDto>())!;
        return (clientId, br.Id, stateId, svc.Id);
    }

    [Fact]
    public async Task Intra_State_Invoice_Splits_Into_Cgst_And_Sgst()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, branchId, stateId, svcId) = await SetupClientBranchServiceAsync(c);

        var create = await c.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoicesController.CreateInvoiceRequest(
                ClientId: clientId, BranchId: branchId,
                PlaceOfSupplyStateId: stateId, // same state → intra
                IssueDate: new DateOnly(2024, 5, 10),
                DueDate: new DateOnly(2024, 6, 10),
                Notes: null,
                LineItems: new()
                {
                    new InvoicesController.LineItemRequest("GST return - Oct 2024",
                        svcId, null, null, 1m, 1000m, 18m)
                }));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var inv = (await create.Content.ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;

        inv.IsIntraState.Should().BeTrue();
        inv.SubtotalInr.Should().Be(1000m);
        inv.CgstInr.Should().Be(90m);
        inv.SgstInr.Should().Be(90m);
        inv.IgstInr.Should().Be(0m);
        inv.TotalInr.Should().Be(1180m);
        inv.Number.Should().StartWith("INV/2024-25/");
        inv.FinancialYear.Should().Be("2024-25");
    }

    [Fact]
    public async Task Inter_State_Invoice_Uses_Igst_Only()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, branchId, _, svcId) = await SetupClientBranchServiceAsync(c);

        var otherState = await c.PostAsJsonAsync("/api/v1/masters/states",
            new StateUpsertDto("OS" + Guid.NewGuid().ToString("N")[..3].ToUpper(),
                "Other " + Guid.NewGuid().ToString("N")[..3], "IN", 0));
        var os = (await otherState.Content.ReadFromJsonAsync<StateDto>())!;

        var create = await c.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoicesController.CreateInvoiceRequest(
                clientId, branchId, os.Id,
                new DateOnly(2024, 5, 10), new DateOnly(2024, 6, 10), null,
                new() { new InvoicesController.LineItemRequest("Advisory", svcId, null, null, 1m, 1000m, 18m) }));
        create.EnsureSuccessStatusCode();
        var inv = (await create.Content.ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;

        inv.IsIntraState.Should().BeFalse();
        inv.CgstInr.Should().Be(0m);
        inv.SgstInr.Should().Be(0m);
        inv.IgstInr.Should().Be(180m);
        inv.TotalInr.Should().Be(1180m);
    }

    [Fact]
    public async Task Issuing_An_Empty_Invoice_Fails()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, branchId, stateId, svcId) = await SetupClientBranchServiceAsync(c);
        var create = await c.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoicesController.CreateInvoiceRequest(clientId, branchId, stateId,
                new DateOnly(2024, 5, 1), new DateOnly(2024, 6, 1), null,
                new() { new InvoicesController.LineItemRequest("placeholder", svcId, null, null, 1m, 100m, 18m) }));
        var inv = (await create.Content.ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;

        // Clear lines then try to issue — should 422.
        var clear = await c.PutAsJsonAsync($"/api/v1/billing/invoices/{inv.Id}",
            new InvoicesController.UpdateInvoiceRequest(inv.IssueDate, inv.DueDate, null, new()));
        clear.EnsureSuccessStatusCode();
        var issue = await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/issue", null);
        issue.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Payment_Transitions_Status_And_Full_Payment_Settles()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, branchId, stateId, svcId) = await SetupClientBranchServiceAsync(c);
        var create = await c.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoicesController.CreateInvoiceRequest(clientId, branchId, stateId,
                new DateOnly(2024, 5, 1), new DateOnly(2024, 6, 1), null,
                new() { new InvoicesController.LineItemRequest("GST", svcId, null, null, 1m, 1000m, 18m) }));
        var inv = (await create.Content.ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/issue", null)).EnsureSuccessStatusCode();

        var pay1 = await c.PostAsJsonAsync($"/api/v1/billing/invoices/{inv.Id}/payments",
            new InvoicesController.RecordPaymentRequest(new DateOnly(2024, 5, 20), 500m, PaymentMode.Upi, "UPI-1", null));
        pay1.EnsureSuccessStatusCode();
        var afterPart = (await pay1.Content.ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
        afterPart.Status.Should().Be(InvoiceStatus.PartPaid);
        afterPart.PaidInr.Should().Be(500m);
        afterPart.OutstandingInr.Should().Be(680m);

        var pay2 = await c.PostAsJsonAsync($"/api/v1/billing/invoices/{inv.Id}/payments",
            new InvoicesController.RecordPaymentRequest(new DateOnly(2024, 5, 25), 680m, PaymentMode.Bank, "RTGS-9", null));
        var afterFull = (await pay2.Content.ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
        afterFull.Status.Should().Be(InvoiceStatus.Paid);
        afterFull.PaidInr.Should().Be(1180m);
        afterFull.OutstandingInr.Should().Be(0m);
    }

    [Fact]
    public async Task Fy_Numbering_Is_Sequential_Within_Fy()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, branchId, stateId, svcId) = await SetupClientBranchServiceAsync(c);
        var dto = new InvoicesController.CreateInvoiceRequest(clientId, branchId, stateId,
            new DateOnly(2024, 5, 1), new DateOnly(2024, 6, 1), null,
            new() { new InvoicesController.LineItemRequest("L", svcId, null, null, 1m, 100m, 18m) });

        var i1 = (await (await c.PostAsJsonAsync("/api/v1/billing/invoices", dto))
            .Content.ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
        var i2 = (await (await c.PostAsJsonAsync("/api/v1/billing/invoices", dto))
            .Content.ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;

        i1.Number.Should().NotBe(i2.Number);
        i1.FinancialYear.Should().Be(i2.FinancialYear).And.Be("2024-25");
        var seq1 = int.Parse(i1.Number.Split('/')[^1]);
        var seq2 = int.Parse(i2.Number.Split('/')[^1]);
        seq2.Should().Be(seq1 + 1);
    }

    [Fact]
    public async Task Fee_Schedule_Resolve_Prefers_Client_Override()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _, _, svcId) = await SetupClientBranchServiceAsync(c);

        var def = await c.PostAsJsonAsync("/api/v1/billing/fee-schedules",
            new FeeSchedulesController.FeeScheduleUpsertRequest(svcId, null,
                1000m, 18m, new DateOnly(2024, 1, 1), null, null));
        def.EnsureSuccessStatusCode();
        var ovr = await c.PostAsJsonAsync("/api/v1/billing/fee-schedules",
            new FeeSchedulesController.FeeScheduleUpsertRequest(svcId, clientId,
                850m, 18m, new DateOnly(2024, 1, 1), null, "client discount"));
        ovr.EnsureSuccessStatusCode();

        var resolved = await c.GetFromJsonAsync<FeeSchedulesController.FeeScheduleDto>(
            $"/api/v1/billing/fee-schedules/resolve?serviceId={svcId}&clientId={clientId}&asOf=2024-06-01");
        resolved!.ClientId.Should().Be(clientId);
        resolved.AmountInr.Should().Be(850m);
    }

    [Fact]
    public async Task Billing_Write_Requires_Permission()
    {
        var staffToken = await _factory.CreateUserAndLoginAsync("billing-staff@cms.local", "StaffUser");
        var staff = _factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);
        // StaffUser has billing.read only — write should 403.
        var r = await staff.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoicesController.CreateInvoiceRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                new DateOnly(2024, 5, 1), new DateOnly(2024, 6, 1), null,
                new() { new InvoicesController.LineItemRequest("x", null, null, null, 1m, 1m, 0m) }));
        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
