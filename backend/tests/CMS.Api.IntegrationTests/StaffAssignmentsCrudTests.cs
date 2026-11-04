using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Assignments;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.StaffAssignmentsTestFixtures;

namespace CMS.Api.IntegrationTests;

public class StaffAssignmentsCrudTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public StaffAssignmentsCrudTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Client_Assign_Creates_Open_Ended_Row_With_Staff_Name()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var staffId = await CreateStaffAsync(c, "Priya");

        var resp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffId, AssignmentRole.Primary, new DateOnly(2024, 4, 1), "Lead CA"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = (await resp.Content.ReadFromJsonAsync<StaffAssignmentService.AssignmentDto>())!;
        dto.Scope.Should().Be(AssignmentScope.Client);
        dto.ScopeId.Should().Be(clientId);
        dto.Role.Should().Be(AssignmentRole.Primary);
        dto.EffectiveFrom.Should().Be(new DateOnly(2024, 4, 1));
        dto.EffectiveTo.Should().BeNull();
        dto.StaffFullName.Should().Contain("Priya");
    }

    [Fact]
    public async Task Reassigning_Same_Staff_To_Same_Role_Returns_409()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var staffId = await CreateStaffAsync(c);

        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffId, AssignmentRole.Primary, new DateOnly(2024, 4, 1), null)))
            .EnsureSuccessStatusCode();

        var dup = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffId, AssignmentRole.Primary, new DateOnly(2024, 5, 1), null));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task List_Default_Only_Shows_Currently_Open_Rows()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var a = await CreateStaffAsync(c, "Anil");
        var b = await CreateStaffAsync(c, "Bhavna");

        var ar = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                a, AssignmentRole.Primary, new DateOnly(2023, 4, 1), null));
        ar.EnsureSuccessStatusCode();

        // Reassign to b — closes a's row, opens b's.
        var br = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                b, AssignmentRole.Primary, new DateOnly(2024, 4, 1), null));
        br.EnsureSuccessStatusCode();

        var currentOnly = await c.GetFromJsonAsync<StaffAssignmentService.AssignmentDto[]>(
            $"/api/v1/clients/{clientId}/staff-assignments");
        currentOnly!.Should().ContainSingle(x => x.StaffId == b);
        currentOnly.Should().NotContain(x => x.StaffId == a);

        var all = await c.GetFromJsonAsync<StaffAssignmentService.AssignmentDto[]>(
            $"/api/v1/clients/{clientId}/staff-assignments?includeHistory=true");
        all!.Should().HaveCount(2);
    }

    [Fact]
    public async Task End_Stamps_EffectiveTo_And_Then_Returns_From_History_Only()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var staffId = await CreateStaffAsync(c);

        var ar = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffId, AssignmentRole.Reviewer, new DateOnly(2024, 4, 1), null));
        var dto = (await ar.Content.ReadFromJsonAsync<StaffAssignmentService.AssignmentDto>())!;

        var end = await c.PostAsync(
            $"/api/v1/clients/{clientId}/staff-assignments/{dto.Id}/end?version={dto.Version}&effectiveTo=2024-09-30", null);
        end.StatusCode.Should().Be(HttpStatusCode.OK);
        var ended = (await end.Content.ReadFromJsonAsync<StaffAssignmentService.AssignmentDto>())!;
        ended.EffectiveTo.Should().Be(new DateOnly(2024, 9, 30));

        var current = await c.GetFromJsonAsync<StaffAssignmentService.AssignmentDto[]>(
            $"/api/v1/clients/{clientId}/staff-assignments");
        current!.Should().BeEmpty();

        var history = await c.GetFromJsonAsync<StaffAssignmentService.AssignmentDto[]>(
            $"/api/v1/clients/{clientId}/staff-assignments?includeHistory=true");
        history!.Should().HaveCount(1);
    }

    [Fact]
    public async Task Ending_Already_Closed_Row_Returns_400()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var staffId = await CreateStaffAsync(c);
        var ar = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffId, AssignmentRole.Primary, new DateOnly(2024, 4, 1), null));
        var dto = (await ar.Content.ReadFromJsonAsync<StaffAssignmentService.AssignmentDto>())!;

        var end1 = await c.PostAsync(
            $"/api/v1/clients/{clientId}/staff-assignments/{dto.Id}/end?version={dto.Version}&effectiveTo=2024-09-30", null);
        var closed = (await end1.Content.ReadFromJsonAsync<StaffAssignmentService.AssignmentDto>())!;

        var end2 = await c.PostAsync(
            $"/api/v1/clients/{clientId}/staff-assignments/{dto.Id}/end?version={closed.Version}&effectiveTo=2024-12-31", null);
        end2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task File_Scope_Assignments_Are_Independent_From_Client_Scope()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, fileId) = await SeedClientAndFileAsync(c);
        var staffA = await CreateStaffAsync(c, "ClientLevel");
        var staffB = await CreateStaffAsync(c, "FileLevel");

        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffA, AssignmentRole.Primary, new DateOnly(2024, 4, 1), null)))
            .EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files/{fileId}/staff-assignments",
            new ServiceFileStaffAssignmentsController.FileAssignRequest(
                staffB, AssignmentRole.Primary, new DateOnly(2024, 4, 1), null)))
            .EnsureSuccessStatusCode();

        var clientLevel = await c.GetFromJsonAsync<StaffAssignmentService.AssignmentDto[]>(
            $"/api/v1/clients/{clientId}/staff-assignments");
        clientLevel!.Should().ContainSingle(x => x.StaffId == staffA);

        var fileLevel = await c.GetFromJsonAsync<StaffAssignmentService.AssignmentDto[]>(
            $"/api/v1/clients/{clientId}/service-files/{fileId}/staff-assignments");
        fileLevel!.Should().ContainSingle(x => x.StaffId == staffB);
    }
}
