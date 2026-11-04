using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CMS.Api.IntegrationTests.ServiceFilesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class TimesheetTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public TimesheetTests(CmsApiFactory f) => _factory = f;

    private async Task<(HttpClient admin, Guid engagementId, Guid serviceId, Guid staffId)> SeedEngagementAsync()
    {
        var admin = await AdminClientAsync(_factory);
        var (clientId, svcId, _, _) = await SeedClientWithServiceAsync(admin);
        var fileResp = await admin.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svcId, null, new DateOnly(2024, 4, 1), null, null));
        var file = (await fileResp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        var engResp = await admin.PostAsJsonAsync(
            $"/api/v1/clients/{clientId}/service-files/{file.Id}/engagements",
            new ServiceFileEngagementsController.CreateEngagementRequest(
                FinancialYear: "2024-25", AssessmentYear: null,
                PeriodType: EngagementPeriodType.Annual, PeriodLabel: null,
                PlannedStartDate: new DateOnly(2024, 4, 1),
                PlannedEndDate: new DateOnly(2025, 3, 31), Notes: null));
        var eng = (await engResp.Content.ReadFromJsonAsync<ServiceFileEngagementsController.EngagementDto>())!;

        // Give the admin a StaffId so TimesheetController permission branches evaluate.
        var staff = (await (await admin.PostAsJsonAsync("/api/v1/masters/staff",
            new StaffUpsertDto("Timesheet Target",
                $"ts-{Guid.NewGuid():N}@tests.cms.local",
                "TS" + Guid.NewGuid().ToString("N")[..5].ToUpper(), null, null))).Content
            .ReadFromJsonAsync<StaffDto>())!;

        return (admin, eng.Id, svcId, staff.Id);
    }

    [Fact]
    public async Task Create_Edit_Approve_Locks_Edit_Then_Unapprove_Allows_Edit_Again()
    {
        var (admin, engagementId, _, staffId) = await SeedEngagementAsync();

        var create = await admin.PostAsJsonAsync("/api/v1/timesheet",
            new TimesheetController.CreateTimeEntryRequest(
                staffId, engagementId, new DateOnly(2024, 5, 6), 90, true, "research"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var entry = (await create.Content.ReadFromJsonAsync<TimesheetController.TimeEntryDto>())!;
        entry.Minutes.Should().Be(90);
        entry.IsApproved.Should().BeFalse();

        var edit = await admin.PutAsJsonAsync($"/api/v1/timesheet/{entry.Id}",
            new TimesheetController.UpdateTimeEntryRequest(120, true, "research + draft"));
        edit.EnsureSuccessStatusCode();

        (await admin.PostAsync($"/api/v1/timesheet/{entry.Id}/approve", null)).EnsureSuccessStatusCode();

        var edit2 = await admin.PutAsJsonAsync($"/api/v1/timesheet/{entry.Id}",
            new TimesheetController.UpdateTimeEntryRequest(45, true, "nope"));
        edit2.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await admin.PostAsync($"/api/v1/timesheet/{entry.Id}/unapprove", null)).EnsureSuccessStatusCode();

        (await admin.PutAsJsonAsync($"/api/v1/timesheet/{entry.Id}",
            new TimesheetController.UpdateTimeEntryRequest(60, false, "corrected")))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Template_Then_Instance_Generation_Is_Lazy_And_Roundtrips_Toggle()
    {
        var (admin, engagementId, serviceId, _) = await SeedEngagementAsync();

        // Admin writes a 3-step template under the service.
        var upsert = await admin.PutAsJsonAsync(
            $"/api/v1/masters/services/{serviceId}/checklist-template",
            new ChecklistTemplatesController.UpsertTemplateRequest("Tax audit", new()
            {
                new ChecklistTemplatesController.TemplateItemInput(0, "Collect trial balance", true),
                new ChecklistTemplatesController.TemplateItemInput(1, "Verify fixed assets", true),
                new ChecklistTemplatesController.TemplateItemInput(2, "Draft audit report", false),
            }));
        upsert.EnsureSuccessStatusCode();

        // First GET lazy-creates the instance.
        var first = await admin.GetFromJsonAsync<EngagementChecklistController.ChecklistDto>(
            $"/api/v1/engagements/{engagementId}/checklist");
        first!.Instantiated.Should().BeTrue();
        first.Items.Should().HaveCount(3);
        first.CompletedItems.Should().Be(0);

        // Toggling an item marks it completed.
        var itemId = first.Items[0].Id;
        var afterToggle = await (await admin.PostAsJsonAsync(
                $"/api/v1/engagements/{engagementId}/checklist/items/{itemId}/toggle",
                new EngagementChecklistController.ToggleRequest("done at site")))
            .Content.ReadFromJsonAsync<EngagementChecklistController.ChecklistDto>();
        afterToggle!.CompletedItems.Should().Be(1);
        afterToggle.Items.First(i => i.Id == itemId).IsCompleted.Should().BeTrue();

        // Re-upsert the template with different items — the already-instantiated checklist keeps
        // its frozen copies.
        (await admin.PutAsJsonAsync($"/api/v1/masters/services/{serviceId}/checklist-template",
            new ChecklistTemplatesController.UpsertTemplateRequest("Tax audit v2", new()
            {
                new ChecklistTemplatesController.TemplateItemInput(0, "Collect trial balance", true),
            })))
            .EnsureSuccessStatusCode();

        var still = await admin.GetFromJsonAsync<EngagementChecklistController.ChecklistDto>(
            $"/api/v1/engagements/{engagementId}/checklist");
        still!.Items.Should().HaveCount(3, "template edits do not retroactively mutate existing instances");
    }

    [Fact]
    public async Task Non_Approvers_Cannot_Approve()
    {
        var (admin, engagementId, _, staffId) = await SeedEngagementAsync();
        var entry = (await (await admin.PostAsJsonAsync("/api/v1/timesheet",
                new TimesheetController.CreateTimeEntryRequest(staffId, engagementId,
                    new DateOnly(2024, 5, 7), 60, true, null))).Content
            .ReadFromJsonAsync<TimesheetController.TimeEntryDto>())!;

        // A StaffUser has timesheet.read + timesheet.write but NOT approve.
        var token = await _factory.CreateUserAndLoginAsync($"ts-nobody-{Guid.NewGuid():N}@tests.cms.local", "StaffUser");
        var staffHttp = _factory.CreateClient();
        staffHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var r = await staffHttp.PostAsync($"/api/v1/timesheet/{entry.Id}/approve", null);
        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Service_With_No_Template_Returns_Empty_Checklist()
    {
        var (admin, engagementId, _, _) = await SeedEngagementAsync();
        var dto = await admin.GetFromJsonAsync<EngagementChecklistController.ChecklistDto>(
            $"/api/v1/engagements/{engagementId}/checklist");
        dto!.Instantiated.Should().BeFalse();
        dto.Items.Should().BeEmpty();
    }
}
