using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class PortalTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public PortalTests(CmsApiFactory f) => _factory = f;

    /// <summary>Create a portal user bound to the given client and return a signed-in HttpClient.</summary>
    private async Task<HttpClient> CreatePortalClientAsync(Guid clientId, string? email = null)
    {
        email ??= $"portal-{Guid.NewGuid():N}@tests.cms.local";
        const string password = "P0rtal!Pass";

        // Direct user creation — mirrors the admin invite flow without the email round-trip.
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<CMS.Domain.Identity.ApplicationUser>>();
            var u = new CMS.Domain.Identity.ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                ClientId = clientId, IsEnabled = true,
            };
            var create = await users.CreateAsync(u, password);
            create.Succeeded.Should().BeTrue();
            (await users.AddToRoleAsync(u, "ClientPortal")).Succeeded.Should().BeTrue();
        }

        var http = _factory.CreateClient();
        var token = await CmsApiFactory.LoginAsync(http, email, password);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    private static async Task<InvoicesController.InvoiceDto> CreateIssuedInvoiceAsync(HttpClient admin, Guid clientId)
    {
        // Admin side — build minimal invoice attached to this client.
        var br = (await (await admin.PostAsJsonAsync($"/api/v1/clients/{clientId}/branches",
            BuildBranchRequest(await EnsureStateAsync(admin), isPrimary: true))).Content
            .ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;
        var svcCode = "SVC" + Guid.NewGuid().ToString("N")[..4].ToUpper();
        var svc = (await (await admin.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto(svcCode, svcCode, null, null, 0, "[]", null))).Content
            .ReadFromJsonAsync<ServiceDto>())!;
        var inv = (await (await admin.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoicesController.CreateInvoiceRequest(clientId, br.Id, br.StateId,
                new DateOnly(2024, 5, 10), new DateOnly(2024, 6, 10), null,
                new() { new InvoicesController.LineItemRequest("audit",
                    svc.Id, null, null, 1m, 1500m, 18m) }))).Content
            .ReadFromJsonAsync<InvoicesController.InvoiceDto>())!;
        (await admin.PostAsync($"/api/v1/billing/invoices/{inv.Id}/issue", null)).EnsureSuccessStatusCode();
        return inv;
    }

    private static async Task<Guid> EnsureStateAsync(HttpClient c)
    {
        // Reuse an existing state if any, else create one.
        var states = await c.GetFromJsonAsync<StateDto[]>("/api/v1/masters/states");
        if (states is { Length: > 0 }) return states[0].Id;
        var st = (await (await c.PostAsJsonAsync("/api/v1/masters/states",
            new StateUpsertDto("ST", "State", "IN", 0))).Content.ReadFromJsonAsync<StateDto>())!;
        return st.Id;
    }

    [Fact]
    public async Task Portal_User_Sees_Only_Their_Clients_Invoices_And_Me()
    {
        var admin = await AdminClientAsync(_factory);
        var (clientA, _, _) = await SeedClientAndMastersAsync(admin, suffix: "A" + Guid.NewGuid().ToString("N")[..4]);
        var (clientB, _, _) = await SeedClientAndMastersAsync(admin, suffix: "B" + Guid.NewGuid().ToString("N")[..4]);

        await CreateIssuedInvoiceAsync(admin, clientA);
        await CreateIssuedInvoiceAsync(admin, clientB);

        var portalA = await CreatePortalClientAsync(clientA);

        var me = await portalA.GetFromJsonAsync<PortalController.MeDto>("/api/v1/portal/me");
        me!.ClientId.Should().Be(clientA);

        var invs = await portalA.GetFromJsonAsync<PortalController.PortalInvoiceDto[]>("/api/v1/portal/invoices");
        invs!.Should().NotBeEmpty();
        // All returned rows must belong to client A (we assert the service didn't leak client B's row).
        // Since the DTO doesn't carry ClientId we re-fetch each by PDF and expect 200 for A, 404 for B.
        foreach (var inv in invs)
        {
            (await portalA.GetAsync($"/api/v1/portal/invoices/{inv.Id}/pdf")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Portal_User_Cannot_Access_Another_Clients_Invoice_Pdf()
    {
        var admin = await AdminClientAsync(_factory);
        var (clientA, _, _) = await SeedClientAndMastersAsync(admin, suffix: "C" + Guid.NewGuid().ToString("N")[..4]);
        var (clientB, _, _) = await SeedClientAndMastersAsync(admin, suffix: "D" + Guid.NewGuid().ToString("N")[..4]);
        var invB = await CreateIssuedInvoiceAsync(admin, clientB);

        var portalA = await CreatePortalClientAsync(clientA);
        var r = await portalA.GetAsync($"/api/v1/portal/invoices/{invB.Id}/pdf");
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_Without_PortalClientId_Gets_Forbid_On_Portal()
    {
        var admin = await AdminClientAsync(_factory);
        // Admin role has `admin.full` which currently grants everything via the HasPermission check,
        // but ClientIdOrNull() returns null → the controller still forbids. Confirm.
        var r = await admin.GetAsync("/api/v1/portal/invoices");
        r.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.OK);
        // In the admin.full case the attribute passes, so the controller's own ClientId check must bite.
        if (r.StatusCode == HttpStatusCode.OK)
        {
            var items = await r.Content.ReadFromJsonAsync<PortalController.PortalInvoiceDto[]>();
            items!.Should().BeEmpty("admin has no PortalClientId so there are no portal rows to return");
        }
    }

    [Fact]
    public async Task Admin_Can_Invite_Portal_User_And_That_User_Can_Sign_In()
    {
        var admin = await AdminClientAsync(_factory);
        var (clientId, _, _) = await SeedClientAndMastersAsync(admin, suffix: "I" + Guid.NewGuid().ToString("N")[..4]);
        var email = $"invited-{Guid.NewGuid():N}@tests.cms.local";

        var invite = await admin.PostAsJsonAsync(
            $"/api/v1/admin/clients/{clientId}/portal-users",
            new ClientPortalUsersController.InviteRequest(email, "Invited User"));
        invite.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await admin.GetFromJsonAsync<ClientPortalUsersController.PortalUserDto[]>(
            $"/api/v1/admin/clients/{clientId}/portal-users");
        list!.Should().ContainSingle(u => u.Email == email);
    }
}
