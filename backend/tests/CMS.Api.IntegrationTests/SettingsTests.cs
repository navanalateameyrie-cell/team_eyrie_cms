using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class SettingsTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public SettingsTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task List_Returns_All_Known_Keys_With_Defaults_When_No_Rows_Exist()
    {
        var c = await AdminClientAsync(_factory);
        var items = await c.GetFromJsonAsync<SettingsController.SettingDto[]>("/api/v1/settings");
        items!.Select(i => i.Key).Should().BeEquivalentTo(new[] { "firmProfile", "mail", "reminders", "eInvoicing", "digitalSignature" });
    }

    [Fact]
    public async Task Roundtrip_Firm_Profile_Persists_And_Reads_Back()
    {
        var c = await AdminClientAsync(_factory);

        var updated = await c.PutAsJsonAsync("/api/v1/settings/firmProfile", new {
            value = new {
                name = "Nava Nala Associates", line1 = "42 Main Rd", city = "Chennai",
                state = "Tamilnadu", pincode = "600001", gstin = "33AAACN0123A1Z5",
                pan = "AAACN0123A", phone = "+91 1234567890", email = "firm@example.com"
            }});
        updated.EnsureSuccessStatusCode();

        var fetched = await c.GetFromJsonAsync<SettingsController.SettingDto>("/api/v1/settings/firmProfile");
        fetched!.UpdatedAtUtc.Should().NotBeNull();
        var inner = (JsonElement)fetched.Value;
        inner.GetProperty("name").GetString().Should().Be("Nava Nala Associates");
        inner.GetProperty("gstin").GetString().Should().Be("33AAACN0123A1Z5");
    }

    [Fact]
    public async Task Pdf_Picks_Up_Updated_Firm_Profile_Without_Restart()
    {
        var c = await AdminClientAsync(_factory);
        // Seed an invoice.
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
                    svc.Id, null, null, 1m, 1500m, 18m) }))).Content
            .ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;

        var pdf1 = await (await c.GetAsync($"/api/v1/billing/invoices/{inv.Id}/pdf")).Content.ReadAsByteArrayAsync();

        // Update firm profile.
        (await c.PutAsJsonAsync("/api/v1/settings/firmProfile", new {
            value = new { name = "A Different Firm Name " + Guid.NewGuid().ToString("N")[..6] } }))
            .EnsureSuccessStatusCode();

        var pdf2 = await (await c.GetAsync($"/api/v1/billing/invoices/{inv.Id}/pdf")).Content.ReadAsByteArrayAsync();

        // Different firm name → different PDF bytes. PDF streams are compressed so a byte comparison
        // is the simplest "the renderer saw the update" assertion that doesn't need text extraction.
        pdf2.Should().NotEqual(pdf1);
    }

    [Fact]
    public async Task Invalid_Json_Shape_Is_Rejected_With_422()
    {
        var c = await AdminClientAsync(_factory);
        // Shape mismatch: enabled must be bool but we're sending a string.
        var payload = new { value = new { enabled = "not-a-bool" } };
        var r = await c.PutAsJsonAsync("/api/v1/settings/reminders", payload);
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Unknown_Setting_Key_Returns_404()
    {
        var c = await AdminClientAsync(_factory);
        var r = await c.PutAsJsonAsync("/api/v1/settings/bogus", new { value = new { } });
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Settings_Require_Settings_Manage_Permission()
    {
        // StaffUser does NOT get settings.manage per the dev seed.
        var token = await _factory.CreateUserAndLoginAsync("settings-nobody@cms.local", "StaffUser");
        var staffHttp = _factory.CreateClient();
        staffHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await staffHttp.GetAsync("/api/v1/settings");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
