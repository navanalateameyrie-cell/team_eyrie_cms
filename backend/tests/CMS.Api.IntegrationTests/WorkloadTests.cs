using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.StaffAssignmentsTestFixtures;

namespace CMS.Api.IntegrationTests;

public class WorkloadTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public WorkloadTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Summary_Counts_Each_Role_Across_Client_And_File_Scopes()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, fileId) = await SeedClientAndFileAsync(c);
        var staffId = await CreateStaffAsync(c, "Workload");

        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffId, AssignmentRole.Primary, new DateOnly(2024, 4, 1), null))).EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files/{fileId}/staff-assignments",
            new ServiceFileStaffAssignmentsController.FileAssignRequest(
                staffId, AssignmentRole.Reviewer, new DateOnly(2024, 4, 1), null))).EnsureSuccessStatusCode();

        var summaries = await c.GetFromJsonAsync<WorkloadController.StaffWorkloadSummary[]>("/api/v1/workload");
        var mine = summaries!.Single(s => s.StaffId == staffId);
        mine.ClientPrimaryCount.Should().Be(1);
        mine.FileReviewerCount.Should().Be(1);
        mine.TotalClients.Should().Be(1);
        mine.TotalFiles.Should().Be(1);
    }

    [Fact]
    public async Task Detail_Returns_Client_And_File_Rows_With_Labels()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, fileId) = await SeedClientAndFileAsync(c);
        var staffId = await CreateStaffAsync(c, "DetailUser");

        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffId, AssignmentRole.Primary, new DateOnly(2024, 4, 1), null))).EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files/{fileId}/staff-assignments",
            new ServiceFileStaffAssignmentsController.FileAssignRequest(
                staffId, AssignmentRole.Primary, new DateOnly(2024, 4, 1), null))).EnsureSuccessStatusCode();

        var detail = await c.GetFromJsonAsync<WorkloadController.StaffWorkloadDetail>(
            $"/api/v1/staff/{staffId}/workload");
        detail!.Clients.Should().ContainSingle().Which.ScopeId.Should().Be(clientId);
        detail.Clients[0].ScopeLabel.Should().NotBeNullOrWhiteSpace();
        detail.ServiceFiles.Should().ContainSingle().Which.ScopeId.Should().Be(fileId);
        detail.ServiceFiles[0].ScopeCode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Workload_Honors_AsOf_And_IncludeHistory()
    {
        var c = await AdminClientAsync(_factory);
        var (clientId, _) = await SeedClientAndFileAsync(c);
        var staffId = await CreateStaffAsync(c, "History");

        var ar = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/staff-assignments",
            new ClientStaffAssignmentsController.AssignRequest(
                staffId, AssignmentRole.Primary, new DateOnly(2023, 4, 1), null));
        var dto = (await ar.Content.ReadFromJsonAsync<CMS.Api.Controllers.Assignments.StaffAssignmentService.AssignmentDto>())!;

        // Close it in the past.
        var end = await c.PostAsync(
            $"/api/v1/clients/{clientId}/staff-assignments/{dto.Id}/end?version={dto.Version}&effectiveTo=2023-09-30", null);
        end.EnsureSuccessStatusCode();

        var current = await c.GetFromJsonAsync<WorkloadController.StaffWorkloadDetail>(
            $"/api/v1/staff/{staffId}/workload");
        current!.Clients.Should().BeEmpty();

        var withHistory = await c.GetFromJsonAsync<WorkloadController.StaffWorkloadDetail>(
            $"/api/v1/staff/{staffId}/workload?includeHistory=true");
        withHistory!.Clients.Should().HaveCount(1);

        var snapshot = await c.GetFromJsonAsync<WorkloadController.StaffWorkloadDetail>(
            $"/api/v1/staff/{staffId}/workload?asOf=2023-06-01");
        snapshot!.Clients.Should().HaveCount(1);
    }
}
