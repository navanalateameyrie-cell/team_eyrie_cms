using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Auditing;
using CMS.Domain.Entities;
using CMS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class AuditExplorerAndImmutabilityTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public AuditExplorerAndImmutabilityTests(CmsApiFactory f) => _factory = f;

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task AuditEntries_Are_Strictly_Immutable_And_Cannot_Be_Modified_Or_Deleted()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        // 1. Create an audit entry
        var entry = AuditEntry.ForEntityChange(
            entityType: "Client",
            entityId: Guid.NewGuid().ToString(),
            action: AuditAction.Create,
            changedFieldsJson: "[\"LegalName\"]",
            oldValuesJson: null,
            newValuesJson: "{\"LegalName\":\"Test Corp\"}",
            userId: Guid.NewGuid(),
            userName: "admin@cms.local",
            correlationId: "corr-immutability-1",
            ipAddress: "127.0.0.1",
            userAgent: "TestClient");

        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync();

        // 2. Attempt to update the entry state to Modified -> SaveChanges MUST throw
        db.Entry(entry).State = EntityState.Modified;
        var actModify = async () => await db.SaveChangesAsync();
        await actModify.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Audit records are append-only and cannot be modified or deleted.*");

        // Detach to reset tracking state
        db.Entry(entry).State = EntityState.Detached;

        // Re-query entry and attempt to remove it -> SaveChanges MUST throw
        var reloaded = await db.AuditEntries.SingleAsync(a => a.Id == entry.Id);
        db.AuditEntries.Remove(reloaded);
        var actDelete = async () => await db.SaveChangesAsync();
        await actDelete.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Audit records are append-only and cannot be modified or deleted.*");
    }

    [Fact]
    public async Task Audit_List_Filter_By_Client_Returns_Client_And_Related_Entities()
    {
        var http = await AdminClientAsync();

        // 1. Create a client with a branch
        var clientResp = await http.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            LegalName: "Audit Test Client " + Guid.NewGuid().ToString("N")[..6],
            EntityTypeId: (await GetFirstEntityTypeIdAsync(http)),
            TradeName: "Audit Trade",
            Pan: "ABCDE" + Random.Shared.Next(1000, 9999) + "F",
            ClientGroupId: null,
            LegacyClientId: "LEG-" + Guid.NewGuid().ToString("N")[..6]));
        clientResp.EnsureSuccessStatusCode();
        var clientDto = (await clientResp.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        // 2. Create a branch for that client
        var stateId = await GetFirstStateIdAsync(http);
        var branchResp = await http.PostAsJsonAsync($"/api/v1/clients/{clientDto.Id}/branches",
            new ClientBranchesController.CreateBranchRequest(
                Code: "BR01",
                Name: "Head Office",
                Type: BranchType.HeadOffice,
                StateId: stateId,
                Address: new ClientBranchesController.AddressDto("123 Main St", null, "Mumbai", "400001"),
                OpenedOn: DateOnly.FromDateTime(DateTime.UtcNow),
                IsPrimary: true,
                IsBillingEnabled: true));
        branchResp.EnsureSuccessStatusCode();
        var branchDto = (await branchResp.Content.ReadFromJsonAsync<ClientBranchesController.BranchDto>())!;

        // 3. Query audit logs with clientId filter
        var auditResp = await http.GetFromJsonAsync<AuditController.ListResponse>(
            $"/api/v1/audit?clientId={clientDto.Id}&take=50");
        auditResp.Should().NotBeNull();
        auditResp!.Items.Should().NotBeEmpty();

        // Should contain client creation audit entry
        var clientAudit = auditResp.Items.FirstOrDefault(a => a.EntityType == "Client" && a.EntityId == clientDto.Id.ToString());
        clientAudit.Should().NotBeNull();
        clientAudit!.Diff.Should().NotBeNull();
        clientAudit.Diff!.Should().Contain(d => d.Field == "LegalName" && d.NewValue == clientDto.LegalName);

        // Should also contain branch creation audit entry
        var branchAudit = auditResp.Items.FirstOrDefault(a => a.EntityType == "Branch" && a.EntityId == branchDto.Id.ToString());
        branchAudit.Should().NotBeNull();
        branchAudit!.Diff.Should().NotBeNull();
        branchAudit.Diff!.Should().Contain(d => d.Field == "Code" && d.NewValue == "BR01");
    }

    [Fact]
    public async Task Audit_FilterOptions_Returns_Metadata_For_Filters()
    {
        var http = await AdminClientAsync();

        var options = await http.GetFromJsonAsync<AuditController.FilterOptionsDto>("/api/v1/audit/filter-options");
        options.Should().NotBeNull();
        options!.EntityTypes.Should().Contain("Client");
        options.EntityTypes.Should().Contain("Branch");
        options.Actions.Should().Contain(a => a.Name == "Create");
        options.Actions.Should().Contain(a => a.Name == "Update");
        options.Actions.Should().Contain(a => a.Name == "Delete");
    }

    [Fact]
    public async Task Audit_Filters_By_Action_And_Date_Work_Accurately()
    {
        var http = await AdminClientAsync();

        var now = DateTimeOffset.UtcNow;
        var list = await http.GetFromJsonAsync<AuditController.ListResponse>(
            $"/api/v1/audit?action={AuditAction.Create}&toUtc={Uri.EscapeDataString(now.AddDays(1).ToString("o"))}&take=10");
        list.Should().NotBeNull();
        list!.Items.Should().OnlyContain(a => a.Action == AuditAction.Create);
    }

    private static async Task<Guid> GetFirstEntityTypeIdAsync(HttpClient http)
    {
        var list = await http.GetFromJsonAsync<EntityTypeDto[]>("/api/v1/masters/entity-types");
        if (list != null && list.Length > 0) return list[0].Id;

        var res = await http.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto("PVT_LTD", "Private Limited Company", "Description"));
        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<EntityTypeDto>();
        return created!.Id;
    }

    private static async Task<Guid> GetFirstStateIdAsync(HttpClient http)
    {
        var list = await http.GetFromJsonAsync<StateDto[]>("/api/v1/masters/states");
        if (list != null && list.Length > 0) return list[0].Id;

        var res = await http.PostAsJsonAsync("/api/v1/masters/states",
            new StateUpsertDto("MH", "Maharashtra", "IN"));
        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<StateDto>();
        return created!.Id;
    }
}
