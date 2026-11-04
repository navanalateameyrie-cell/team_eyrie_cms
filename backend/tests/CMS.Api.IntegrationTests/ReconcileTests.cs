using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class ReconcileTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ReconcileTests(CmsApiFactory f) => _factory = f;

    private static async Task<InvoicesController.InvoiceDto> SeedIssuedInvoiceAsync(HttpClient c, decimal gross = 1000m)
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
                new() { new InvoicesController.LineItemRequest("audit", svc.Id, null, null, 1m, gross, 18m) }))).Content
            .ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
        (await c.PostAsync($"/api/v1/billing/invoices/{inv.Id}/issue", null)).EnsureSuccessStatusCode();
        return inv;
    }

    private static MultipartFormDataContent CsvUpload(string csv)
    {
        var mp = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes(csv);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        mp.Add(content, "File", "statement.csv");
        return mp;
    }

    [Fact]
    public async Task Upload_Auto_Matches_Line_To_Invoice_By_Reference_And_Amount()
    {
        var c = await AdminClientAsync(_factory);
        var inv = await SeedIssuedInvoiceAsync(c);
        // GST18 of 1000 = 1180 outstanding. Build a CSV with a credit matching that exactly.
        var csv =
            "ValueDate,Description,Reference,Debit,Credit\n" +
            $"2024-05-15,UPI from client,{inv.Number},,1180.00\n";

        var upload = await c.PostAsync("/api/v1/billing/reconcile/upload", CsvUpload(csv));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await upload.Content.ReadFromJsonAsync<ReconcileController.ReconcileUploadResponse>())!;
        body.LineCount.Should().Be(1);
        body.AutoMatched.Should().Be(1);

        var after = await c.GetFromJsonAsync<InvoicesController.InvoiceDto>($"/api/v1/billing/invoices/{inv.Id}");
        after!.Status.Should().Be(InvoiceStatus.Paid);
        after.PaidInr.Should().Be(1180m);
    }

    [Fact]
    public async Task Ambiguous_Amount_Match_Leaves_Line_Unmatched()
    {
        var c = await AdminClientAsync(_factory);
        // Two invoices with identical outstanding (1180) — a credit for 1180 can't be auto-matched.
        var a = await SeedIssuedInvoiceAsync(c);
        var b = await SeedIssuedInvoiceAsync(c);
        a.TotalInr.Should().Be(b.TotalInr);

        var csv =
            "ValueDate,Description,Reference,Debit,Credit\n" +
            "2024-05-15,Vague deposit,,,1180.00\n";
        var upload = await c.PostAsync("/api/v1/billing/reconcile/upload", CsvUpload(csv));
        var body = (await upload.Content.ReadFromJsonAsync<ReconcileController.ReconcileUploadResponse>())!;
        body.AutoMatched.Should().Be(0);
        body.Ambiguous.Should().Be(1);
    }

    [Fact]
    public async Task Manual_Match_Books_A_Payment_And_Flips_Status()
    {
        var c = await AdminClientAsync(_factory);
        // Pick an amount that no other test invoice shares — the auto-matcher narrows candidates
        // by outstanding-amount first, so a unique value guarantees we land in the manual queue.
        var inv = await SeedIssuedInvoiceAsync(c, gross: 7331m);
        var expectedOutstanding = inv.TotalInr;  // 7331 + 18% = 8650.58
        var oddAmount = expectedOutstanding - 100m; // credit for less than outstanding → no auto-match
        var csv =
            "ValueDate,Description,Reference,Debit,Credit\n" +
            $"2024-05-18,Bank transfer,REF-XYZ,,{oddAmount:F2}\n";
        var upload = (await (await c.PostAsync("/api/v1/billing/reconcile/upload", CsvUpload(csv))).Content
            .ReadFromJsonAsync<ReconcileController.ReconcileUploadResponse>())!;
        upload.AutoMatched.Should().Be(0);

        var lines = await c.GetFromJsonAsync<ReconcileController.ReconcileLineDto[]>(
            $"/api/v1/billing/reconcile/batches/{upload.BatchId}/lines?status={(int)BankStatementLineStatus.Unmatched}");
        lines!.Should().ContainSingle();
        var line = lines[0];

        // Match for the line amount — partial payment.
        var match = await c.PostAsJsonAsync($"/api/v1/billing/reconcile/lines/{line.Id}/match-invoice",
            new ReconcileController.MatchInvoiceRequest(inv.Id, line.AmountInr, "manual"));
        match.EnsureSuccessStatusCode();
        var updated = (await match.Content.ReadFromJsonAsync<ReconcileController.ReconcileLineDto>())!;
        updated.Status.Should().Be(BankStatementLineStatus.MatchedInvoice);
        updated.MatchedInvoiceNumber.Should().Be(inv.Number);

        var after = await c.GetFromJsonAsync<InvoicesController.InvoiceDto>($"/api/v1/billing/invoices/{inv.Id}");
        after!.Status.Should().Be(InvoiceStatus.PartPaid);
        after.PaidInr.Should().Be(line.AmountInr);
    }

    [Fact]
    public async Task Ignore_Flips_Status_And_Does_Not_Book_Payment()
    {
        var c = await AdminClientAsync(_factory);
        var csv =
            "ValueDate,Description,Reference,Debit,Credit\n" +
            "2024-05-18,Bank charges,,25.00,\n";
        var upload = (await (await c.PostAsync("/api/v1/billing/reconcile/upload", CsvUpload(csv))).Content
            .ReadFromJsonAsync<ReconcileController.ReconcileUploadResponse>())!;

        var lines = await c.GetFromJsonAsync<ReconcileController.ReconcileLineDto[]>(
            $"/api/v1/billing/reconcile/batches/{upload.BatchId}/lines");
        var line = lines!.Single();
        line.AmountInr.Should().Be(-25m, "debit lines should import with a negative sign");

        var ignore = await c.PostAsJsonAsync($"/api/v1/billing/reconcile/lines/{line.Id}/ignore",
            new ReconcileController.IgnoreLineRequest("bank fee"));
        ignore.EnsureSuccessStatusCode();
        var after = (await ignore.Content.ReadFromJsonAsync<ReconcileController.ReconcileLineDto>())!;
        after.Status.Should().Be(BankStatementLineStatus.Ignored);
    }

    [Fact]
    public async Task Malformed_Csv_Returns_422()
    {
        var c = await AdminClientAsync(_factory);
        var r = await c.PostAsync("/api/v1/billing/reconcile/upload", CsvUpload("Date,Something\n"));
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
