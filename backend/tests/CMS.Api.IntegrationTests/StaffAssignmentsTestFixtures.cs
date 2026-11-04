using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;

namespace CMS.Api.IntegrationTests;

internal static class StaffAssignmentsTestFixtures
{
    public static async Task<HttpClient> AdminClientAsync(CmsApiFactory f)
    {
        var c = f.CreateClient();
        var token = await f.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    public static async Task<Guid> CreateStaffAsync(HttpClient c, string? label = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpper();
        var r = await c.PostAsJsonAsync("/api/v1/masters/staff",
            new StaffUpsertDto($"{label ?? "Staff"} {suffix}", $"user.{suffix}@cms.local",
                               $"S-{suffix[..4]}", null, "Associate"));
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<StaffDto>())!.Id;
    }

    public static async Task<(Guid clientId, Guid fileId)> SeedClientAndFileAsync(HttpClient c)
    {
        var (clientId, serviceId, _, _) = await ServiceFilesTestFixtures.SeedClientWithServiceAsync(c);
        var fileResp = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                serviceId, null, new DateOnly(2024, 4, 1), null, null));
        fileResp.EnsureSuccessStatusCode();
        var file = (await fileResp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        return (clientId, file.Id);
    }
}
