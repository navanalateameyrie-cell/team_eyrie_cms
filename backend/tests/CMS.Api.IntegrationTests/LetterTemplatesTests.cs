using System.Net;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Domain.Entities;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.BranchesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class LetterTemplatesTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public LetterTemplatesTests(CmsApiFactory f) => _factory = f;

    private static async Task<Guid> SeedObligationAsync(HttpClient c)
    {
        var (clientId, _, _) = await SeedClientAndMastersAsync(c);
        var svcCode = "GST" + Guid.NewGuid().ToString("N")[..3].ToUpper();
        var svc = (await (await c.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto(svcCode, svcCode, null, null, 0, "[]", null,
                CMS.Domain.Entities.ComplianceFrequency.Monthly, 20))).Content
            .ReadFromJsonAsync<ServiceDto>())!;
        (await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svc.Id, null, new DateOnly(2024, 4, 1), null, null, MetadataJson: null)))
            .EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync("/api/v1/compliance/generate", new { horizonMonths = 6 }))
            .EnsureSuccessStatusCode();
        var page = await c.GetFromJsonAsync<ComplianceController.Page<ComplianceController.ObligationDto>>(
            "/api/v1/compliance?from=2020-01-01&to=2099-12-31&take=1");
        return page!.Items.First().Id;
    }

    private const string Body =
        "Dear {{client.name}},\n\n" +
        "Please note the compliance obligation for {{service.code}} covering period {{obligation.periodKey}} is due on {{obligation.dueDate}}.\n\n" +
        "Regards,\n{{firm.name}}";

    [Fact]
    public async Task Crud_Roundtrip_And_Duplicate_Name_Returns_Conflict()
    {
        var c = await AdminClientAsync(_factory);
        var name = "Compliance notice " + Guid.NewGuid().ToString("N")[..6];

        var create = await c.PostAsJsonAsync("/api/v1/letter-templates",
            new LetterTemplatesController.LetterTemplateUpsertRequest(name, Body));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await create.Content.ReadFromJsonAsync<LetterTemplatesController.LetterTemplateDto>())!;
        dto.IsActive.Should().BeTrue();

        var dup = await c.PostAsJsonAsync("/api/v1/letter-templates",
            new LetterTemplatesController.LetterTemplateUpsertRequest(name, "x"));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await c.PostAsync($"/api/v1/letter-templates/{dto.Id}/active?value=false", null))
            .EnsureSuccessStatusCode();

        var list = await c.GetFromJsonAsync<LetterTemplatesController.LetterTemplateDto[]>(
            "/api/v1/letter-templates?activeOnly=false");
        list!.Should().Contain(t => t.Id == dto.Id && !t.IsActive);
    }

    [Fact]
    public async Task Preview_Resolves_Known_Tokens_And_Passes_Unknown_Through()
    {
        var c = await AdminClientAsync(_factory);
        var obligationId = await SeedObligationAsync(c);
        var template = (await (await c.PostAsJsonAsync("/api/v1/letter-templates",
                new LetterTemplatesController.LetterTemplateUpsertRequest(
                    "Preview " + Guid.NewGuid().ToString("N")[..6],
                    Body + "\n\nUnknown: {{not.a.real.token}}"))).Content
            .ReadFromJsonAsync<LetterTemplatesController.LetterTemplateDto>())!;

        var preview = await c.GetFromJsonAsync<ObligationLetterController.LetterPreviewDto>(
            $"/api/v1/compliance/{obligationId}/letter/preview?templateId={template.Id}");
        preview!.Body.Should().Contain("Client ");
        preview.Body.Should().NotContain("{{client.name}}");
        preview.Body.Should().Contain("{{not.a.real.token}}", "unknown tokens stay literal");
        preview.Context.Should().ContainKey("client.name");
    }

    [Fact]
    public async Task Pdf_Endpoint_Returns_Nontrivial_Pdf_Bytes()
    {
        var c = await AdminClientAsync(_factory);
        var obligationId = await SeedObligationAsync(c);
        var template = (await (await c.PostAsJsonAsync("/api/v1/letter-templates",
                new LetterTemplatesController.LetterTemplateUpsertRequest(
                    "Pdf " + Guid.NewGuid().ToString("N")[..6], Body))).Content
            .ReadFromJsonAsync<LetterTemplatesController.LetterTemplateDto>())!;

        var resp = await c.GetAsync($"/api/v1/compliance/{obligationId}/letter?templateId={template.Id}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Unknown_Template_Returns_404_On_Preview()
    {
        var c = await AdminClientAsync(_factory);
        var obligationId = await SeedObligationAsync(c);
        var r = await c.GetAsync(
            $"/api/v1/compliance/{obligationId}/letter/preview?templateId={Guid.NewGuid()}");
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
