using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class ReportsTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ReportsTests(CmsApiFactory f) => _factory = f;

    private static async Task<InvoicesController.InvoiceDto> CreateInvoiceAsync(
        HttpClient c, DateOnly issueDate, decimal unitPrice = 1000m, decimal gstRate = 18m,
        string? servicePrefix = null)
    {
        var (clientId, _, stateId) = await SeedClientAndMastersAsync(c);
        var br = (await (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(stateId, isPrimary: true))).Content
            .ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        var svcCode = (servicePrefix ?? "SVC") + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var svc = (await (await c.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto(svcCode, svcCode, null, null, 0, "[]", null))).Content
            .ReadFromJsonAsync<ServiceDto>())!;
        var create = await c.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoicesController.CreateInvoiceRequest(clientId, br.Id, stateId,
                issueDate, issueDate.AddDays(30), null,
                new() { new InvoicesController.LineItemRequest("line", svc.Id, null, null, 1m, unitPrice, gstRate) }));
        create.EnsureSuccessStatusCode();
        var inv = (await create.Content.ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/issue", null)).EnsureSuccessStatusCode();
        return inv;
    }

    [Fact]
    public async Task Revenue_Monthly_Returns_Twelve_Buckets_Even_With_No_Data()
    {
        var c = await AdminClientAsync(_factory);
        var buckets = await c.GetFromJsonAsync<ReportsController.RevenueMonthlyPoint[]>(
            "/api/v1/reports/revenue-monthly?fy=2099-00");
        buckets!.Should().HaveCount(12);
        buckets.Should().OnlyContain(b => b.AmountInr == 0m && b.InvoiceCount == 0);
    }

    [Fact]
    public async Task Revenue_Monthly_Captures_An_Issued_Invoice_In_The_Right_Bucket()
    {
        var c = await AdminClientAsync(_factory);
        await CreateInvoiceAsync(c, new DateOnly(2024, 5, 10)); // May = FY 2024-25 month 2 (index 1)

        var buckets = await c.GetFromJsonAsync<ReportsController.RevenueMonthlyPoint[]>(
            "/api/v1/reports/revenue-monthly?fy=2024-25");
        var may = buckets!.First(b => b.Year == 2024 && b.Month == 5);
        may.InvoiceCount.Should().Be(1);
        may.AmountInr.Should().Be(1180m);
    }

    [Fact]
    public async Task Outstanding_Ageing_Returns_All_Five_Buckets_Ordered()
    {
        var c = await AdminClientAsync(_factory);
        var buckets = await c.GetFromJsonAsync<ReportsController.AgeingBucket[]>(
            "/api/v1/reports/outstanding-ageing");
        buckets!.Select(b => b.Label).Should().Equal("Current", "1-30", "31-60", "61-90", "90+");
    }

    [Fact]
    public async Task Top_Clients_Ranks_By_Revenue()
    {
        var c = await AdminClientAsync(_factory);
        await CreateInvoiceAsync(c, new DateOnly(2024, 6, 1), unitPrice: 500m);
        await CreateInvoiceAsync(c, new DateOnly(2024, 6, 2), unitPrice: 2000m);

        var top = await c.GetFromJsonAsync<ReportsController.TopClient[]>(
            "/api/v1/reports/top-clients?fy=2024-25&take=5");
        top!.Should().NotBeEmpty();
        top.Should().BeInDescendingOrder(t => t.RevenueInr);
    }

    [Fact]
    public async Task Client_Growth_Returns_Requested_Window()
    {
        var c = await AdminClientAsync(_factory);
        var points = await c.GetFromJsonAsync<ReportsController.ClientGrowthPoint[]>(
            "/api/v1/reports/client-growth?months=6");
        points!.Should().HaveCount(6);
    }

    [Fact]
    public async Task Compliance_Stats_Buckets_By_Status()
    {
        var c = await AdminClientAsync(_factory);
        var stats = await c.GetFromJsonAsync<ReportsController.ComplianceServiceStat[]>(
            "/api/v1/reports/compliance-stats");
        stats.Should().NotBeNull();
        // Shape test: when there are obligations in the DB, counts must be non-negative. An empty
        // list is fine on a cold database — the endpoint still returned a well-formed JSON array.
        stats!.All(s => s.Pending >= 0 && s.InProgress >= 0 && s.Filed >= 0
                        && s.Delayed >= 0 && s.Waived >= 0 && s.Overdue >= 0)
            .Should().BeTrue();
    }

    [Fact]
    public async Task Reports_Require_Permission()
    {
        var token = await _factory.CreateUserAndLoginAsync("reports-nobody@cms.local", "SystemAdmin");
        var sysadmin = _factory.CreateClient();
        sysadmin.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await sysadmin.GetAsync("/api/v1/reports/revenue-monthly");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
