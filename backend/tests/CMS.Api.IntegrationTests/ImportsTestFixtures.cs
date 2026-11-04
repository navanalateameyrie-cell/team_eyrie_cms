using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;

namespace CMS.Api.IntegrationTests;

internal static class ImportsTestFixtures
{
    public static async Task<HttpClient> AdminClientAsync(CmsApiFactory f)
    {
        var c = f.CreateClient();
        var token = await f.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    public static byte[] BuildClientsSheet(IEnumerable<Dictionary<string, string>> rows,
                                           IEnumerable<string>? extraHeaders = null)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Clients");

        var allHeaders = new[] { "LegalName", "TradeName", "PAN", "LegacyClientId", "EntityTypeCode", "ClientGroupCode" }
            .Concat(extraHeaders ?? Array.Empty<string>()).ToArray();
        for (int i = 0; i < allHeaders.Length; i++)
            ws.Cell(1, i + 1).Value = allHeaders[i];

        int r = 2;
        foreach (var row in rows)
        {
            for (int i = 0; i < allHeaders.Length; i++)
                if (row.TryGetValue(allHeaders[i], out var v)) ws.Cell(r, i + 1).Value = v;
            r++;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] BuildFinalSheet(IEnumerable<string> serviceCodes,
                                         IEnumerable<Dictionary<string, string>> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Final");
        var fixedHeaders = new[] { "LegalName", "PAN", "LegacyClientId" };
        var svcHeaders = serviceCodes.Select(c => $"Service_{c}").ToArray();
        var all = fixedHeaders.Concat(svcHeaders).ToArray();
        for (int i = 0; i < all.Length; i++) ws.Cell(1, i + 1).Value = all[i];

        int r = 2;
        foreach (var row in rows)
        {
            for (int i = 0; i < all.Length; i++)
                if (row.TryGetValue(all[i], out var v)) ws.Cell(r, i + 1).Value = v;
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static async Task<Guid> UploadBytesAsync(HttpClient c, byte[] bytes, string fileName)
    {
        using var multi = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        multi.Add(fileContent, "file", fileName);
        var resp = await c.PostAsync("/api/v1/imports/upload", multi);
        resp.EnsureSuccessStatusCode();
        var dto = (await resp.Content.ReadFromJsonAsync<ImportsController.UploadResponse>())!;
        return dto.BatchId;
    }

    public static async Task SeedEntityTypeAndServiceAsync(HttpClient c, string entityCode, string serviceCode)
    {
        var et = await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto(entityCode, $"Entity {entityCode}", null));
        if (et.StatusCode != System.Net.HttpStatusCode.Conflict) et.EnsureSuccessStatusCode();
        var sv = await c.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto(serviceCode, $"Service {serviceCode}", null, null, 0));
        if (sv.StatusCode != System.Net.HttpStatusCode.Conflict) sv.EnsureSuccessStatusCode();
    }
}
