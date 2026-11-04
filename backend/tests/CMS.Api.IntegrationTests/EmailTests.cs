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

public class EmailTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public EmailTests(CmsApiFactory f) => _factory = f;

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
                new DateOnly(2024, 5, 10), new DateOnly(2024, 6, 10), null,
                new() { new InvoicesController.LineItemRequest("line", svc.Id, null, null, 1m, 1000m, 18m) }))).Content
            .ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
        return inv.Id;
    }

    [Fact]
    public async Task Send_Email_Drops_Eml_File_And_Records_Sent_Outbox_Row()
    {
        var c = await AdminClientAsync(_factory);
        var id = await CreateInvoiceAsync(c);
        var filesBefore = Directory.Exists(_factory.MailDropFolder)
            ? Directory.GetFiles(_factory.MailDropFolder, "*.eml") : Array.Empty<string>();

        var resp = await c.PostAsJsonAsync($"/api/v1/billing/invoices/{id}/send-email",
            new InvoicesController.SendEmailRequest(
                To: new List<string> { "client@example.com" },
                Cc: null, Subject: null, Body: null));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await resp.Content.ReadFromJsonAsync<InvoicesController.SendEmailResponse>())!;
        body.Status.Should().Be(EmailStatus.Sent);
        body.ErrorMessage.Should().BeNull();

        var filesAfter = Directory.GetFiles(_factory.MailDropFolder, "*.eml");
        filesAfter.Length.Should().BeGreaterThan(filesBefore.Length,
            "the dev FileSystemEmailSender should have written a new .eml under the drop folder");

        var text = await File.ReadAllTextAsync(filesAfter.Except(filesBefore).First());
        text.Should().Contain("client@example.com");
        text.Should().Contain("application/pdf");
    }

    [Fact]
    public async Task Send_Email_Requires_To_List()
    {
        var c = await AdminClientAsync(_factory);
        var id = await CreateInvoiceAsync(c);
        var resp = await c.PostAsJsonAsync($"/api/v1/billing/invoices/{id}/send-email",
            new InvoicesController.SendEmailRequest(To: new List<string>(), Cc: null, Subject: null, Body: null));
        // ASP.NET model validation ([MinLength(1)]) rejects empty To with 400 before controller code runs.
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Send_Email_404_For_Unknown_Invoice()
    {
        var c = await AdminClientAsync(_factory);
        var resp = await c.PostAsJsonAsync($"/api/v1/billing/invoices/{Guid.NewGuid()}/send-email",
            new InvoicesController.SendEmailRequest(
                To: new List<string> { "a@b.com" }, Cc: null, Subject: null, Body: null));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Outbox_List_Returns_Sent_Entry_And_Filters_By_Invoice()
    {
        var c = await AdminClientAsync(_factory);
        var id = await CreateInvoiceAsync(c);
        (await c.PostAsJsonAsync($"/api/v1/billing/invoices/{id}/send-email",
            new InvoicesController.SendEmailRequest(
                To: new List<string> { "someone@example.com" }, Cc: null, Subject: "Hello", Body: "Body text")))
            .EnsureSuccessStatusCode();

        var page = await c.GetFromJsonAsync<EmailOutboxController.OutboxPage>(
            $"/api/v1/email-outbox?invoiceId={id}");
        page!.Items.Should().ContainSingle(i => i.Subject == "Hello" && i.Status == EmailStatus.Sent);
        page.Items[0].AttachmentCount.Should().Be(1);
        page.Items[0].ToAddresses.Should().Contain("someone@example.com");
    }

    [Fact]
    public async Task Send_Email_Requires_Email_Send_Permission()
    {
        // StaffUser does not get email.send in the dev seed.
        var staffToken = await _factory.CreateUserAndLoginAsync("email-staff@cms.local", "StaffUser");
        var staff = _factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);

        var admin = await AdminClientAsync(_factory);
        var id = await CreateInvoiceAsync(admin);
        var resp = await staff.PostAsJsonAsync($"/api/v1/billing/invoices/{id}/send-email",
            new InvoicesController.SendEmailRequest(
                To: new List<string> { "x@y.com" }, Cc: null, Subject: null, Body: null));
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
