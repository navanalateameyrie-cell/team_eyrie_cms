using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Assignments;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.StaffAssignmentsTestFixtures;

/// <summary>
/// Enforces the Phase 9 invariant: reassignment NEVER rewrites a historical row's StaffId / Role /
/// EffectiveFrom. The only field it may mutate on an existing row is EffectiveTo, exactly once.
/// </summary>
namespace CMS.Api.IntegrationTests;

public class StaffAssignmentsReassignmentTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public StaffAssignmentsReassignmentTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Reassigning_Primary_Closes_Old_Row_And_Opens_New_Row()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var oldStaff = await CreateStaffAsync(c, "Old");
        var newStaff = await CreateStaffAsync(c, "New");

        var r1 = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                oldStaff, AssignmentRole.Primary, new DateOnly(2023, 4, 1), "initial"));
        var original = (await r1.Content.ReadFromJsonAsync<StaffAssignmentService.AssignmentDto>())!;

        var r2 = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                newStaff, AssignmentRole.Primary, new DateOnly(2024, 4, 1), "handover"));
        r2.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await c.GetFromJsonAsync<StaffAssignmentService.AssignmentDto[]>(
            $"/api/v1/clients/{clientId}/staff-assignments?includeHistory=true");
        history!.Should().HaveCount(2);

        var closed = history.Single(x => x.StaffId == oldStaff);
        closed.Id.Should().Be(original.Id, "the old row must still exist, not be deleted and recreated");
        closed.EffectiveFrom.Should().Be(new DateOnly(2023, 4, 1), "EffectiveFrom is immutable");
        closed.Role.Should().Be(AssignmentRole.Primary, "Role is immutable");
        closed.EffectiveTo.Should().Be(new DateOnly(2024, 3, 31),
            "the only legal mutation stamps EffectiveTo = new.EffectiveFrom - 1 day");

        var open = history.Single(x => x.StaffId == newStaff);
        open.EffectiveFrom.Should().Be(new DateOnly(2024, 4, 1));
        open.EffectiveTo.Should().BeNull();
    }

    [Fact]
    public async Task Reassigning_Reviewer_Follows_The_Same_Close_And_Insert_Rule()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var r1 = await CreateStaffAsync(c, "Reviewer1");
        var r2 = await CreateStaffAsync(c, "Reviewer2");

        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                r1, AssignmentRole.Reviewer, new DateOnly(2023, 4, 1), null))).EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                r2, AssignmentRole.Reviewer, new DateOnly(2024, 4, 1), null))).EnsureSuccessStatusCode();

        var current = await c.GetFromJsonAsync<StaffAssignmentService.AssignmentDto[]>(
            $"/api/v1/clients/{clientId}/staff-assignments?role=Reviewer");
        current!.Should().ContainSingle().Which.StaffId.Should().Be(r2);
    }

    [Fact]
    public async Task Secondaries_May_Have_Multiple_Current_Staff_Without_Closing_Each_Other()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var sa = await CreateStaffAsync(c, "SecA");
        var sb = await CreateStaffAsync(c, "SecB");
        var sc = await CreateStaffAsync(c, "SecC");

        foreach (var sid in new[] { sa, sb, sc })
        {
            var r = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
                new ClientStaffAssignmentsController.AssignRequest(
                    sid, AssignmentRole.Secondary, new DateOnly(2024, 4, 1), null));
            r.EnsureSuccessStatusCode();
        }

        var current = await c.GetFromJsonAsync<StaffAssignmentService.AssignmentDto[]>(
            $"/api/v1/clients/{clientId}/staff-assignments?role=Secondary");
        current!.Should().HaveCount(3);
        current.Select(x => x.StaffId).Should().BeEquivalentTo(new[] { sa, sb, sc });
    }

    [Fact]
    public async Task Assigning_Same_Staff_Twice_As_Secondary_Returns_409()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var staffId = await CreateStaffAsync(c);

        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffId, AssignmentRole.Secondary, new DateOnly(2024, 4, 1), null)))
            .EnsureSuccessStatusCode();
        var dup = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffId, AssignmentRole.Secondary, new DateOnly(2024, 5, 1), null));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AsOf_Reconstructs_Historical_Snapshot()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var oldStaff = await CreateStaffAsync(c, "Yesterday");
        var newStaff = await CreateStaffAsync(c, "Today");

        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                oldStaff, AssignmentRole.Primary, new DateOnly(2023, 4, 1), null))).EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                newStaff, AssignmentRole.Primary, new DateOnly(2024, 4, 1), null))).EnsureSuccessStatusCode();

        var snapshot2023 = await c.GetFromJsonAsync<StaffAssignmentService.AssignmentDto[]>(
            $"/api/v1/clients/{clientId}/staff-assignments?asOf=2023-10-01");
        snapshot2023!.Should().ContainSingle().Which.StaffId.Should().Be(oldStaff);

        var snapshotToday = await c.GetFromJsonAsync<StaffAssignmentService.AssignmentDto[]>(
            $"/api/v1/clients/{clientId}/staff-assignments");
        snapshotToday!.Should().ContainSingle().Which.StaffId.Should().Be(newStaff);
    }
}
