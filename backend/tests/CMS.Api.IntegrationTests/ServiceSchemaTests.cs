using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Application.Clients;
using FluentAssertions;
using Xunit;
using static CMS.Api.IntegrationTests.ServiceFilesTestFixtures;

namespace CMS.Api.IntegrationTests;

public class ServiceSchemaTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ServiceSchemaTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public void Validator_Flags_Required_Missing_And_Pattern_Mismatch()
    {
        var required = ServiceFieldSerializer.WriteFieldDefs(new[]
        {
            new ServiceFieldDef("fy", "FY", "string", Required: true, Pattern: @"^\d{4}-\d{2}$"),
            new ServiceFieldDef("period", "Period", "select", Required: true, Options: new[] { "M", "Q" }),
        });
        var optional = ServiceFieldSerializer.WriteFieldDefs(new[]
        {
            new ServiceFieldDef("notes", "Notes", "string"),
        });

        var errs = ServiceFileMetadataValidator.Validate(required, optional,
            "{\"fy\":\"badvalue\",\"period\":\"X\"}");

        errs.Should().Contain(e => e.FieldName == "fy" && e.Code == "pattern_mismatch");
        errs.Should().Contain(e => e.FieldName == "period" && e.Code == "not_in_options");
    }

    [Fact]
    public void Validator_Accepts_Fully_Valid_Metadata()
    {
        var required = ServiceFieldSerializer.WriteFieldDefs(new[]
        {
            new ServiceFieldDef("fy", "FY", "string", Required: true, Pattern: @"^\d{4}-\d{2}$"),
        });
        var errs = ServiceFileMetadataValidator.Validate(required, null,
            "{\"fy\":\"2024-25\"}");
        errs.Should().BeEmpty();
    }

    [Fact]
    public async Task Service_Create_Carries_Schema_And_Get_Schema_Returns_Parsed_Defs()
    {
        var c = await AdminClientAsync(_factory);
        var code = $"SCH-{Guid.NewGuid().ToString("N")[..5].ToUpper()}";
        var requiredJson = ServiceFieldSerializer.WriteFieldDefs(new[]
        {
            new ServiceFieldDef("foo", "Foo", "string", Required: true),
        });

        var resp = await c.PostAsJsonAsync("/api/v1/masters/services", new ServiceUpsertDto(
            Code: code, Name: $"Schema svc {code}", Category: null, Description: null,
            DisplayOrder: 0, RequiredFieldsJson: requiredJson, OptionalFieldsJson: null));
        resp.EnsureSuccessStatusCode();
        var dto = (await resp.Content.ReadFromJsonAsync<ServiceDto>())!;

        var schemaResp = await c.GetAsync($"/api/v1/masters/services/{dto.Id}/schema");
        schemaResp.EnsureSuccessStatusCode();
        var schema = JsonDocument.Parse(await schemaResp.Content.ReadAsStringAsync()).RootElement;
        schema.GetProperty("code").GetString().Should().Be(code);
        var reqArr = schema.GetProperty("required").EnumerateArray().ToArray();
        reqArr.Should().ContainSingle();
        reqArr[0].GetProperty("name").GetString().Should().Be("foo");
    }

    [Fact]
    public async Task Service_File_Create_Fails_422_When_Required_Metadata_Missing()
    {
        var c = await AdminClientAsync(_factory);
        // Seed a service with a required field.
        var requiredJson = ServiceFieldSerializer.WriteFieldDefs(new[]
        {
            new ServiceFieldDef("bankName", "Bank name", "string", Required: true),
        });
        var svcResp = await c.PostAsJsonAsync("/api/v1/masters/services", new ServiceUpsertDto(
            Code: $"BK-{Guid.NewGuid().ToString("N")[..5].ToUpper()}", Name: "Bank Audit test",
            Category: null, Description: null, DisplayOrder: 0,
            RequiredFieldsJson: requiredJson, OptionalFieldsJson: null));
        svcResp.EnsureSuccessStatusCode();
        var svc = (await svcResp.Content.ReadFromJsonAsync<ServiceDto>())!;

        // Minimal client fixture.
        var (clientId, _, _, _) = await ServiceFilesTestFixtures.SeedClientWithServiceAsync(c);

        // POST a file with NO metadata — should 422.
        var missing = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svc.Id, null, new DateOnly(2024, 4, 1), null, null, MetadataJson: null));
        missing.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // POST a file WITH the required field — should succeed.
        var ok = await c.PostAsJsonAsync($"/api/v1/clients/{clientId}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svc.Id, null, new DateOnly(2024, 4, 1), null, null,
                MetadataJson: "{\"bankName\":\"ACME Bank\"}"));
        ok.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await ok.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;
        dto.MetadataJson.Should().Contain("ACME Bank");
    }

    [Fact]
    public async Task Rich_DTO_Includes_Client_Pan_And_Group_Name()
    {
        var c = await AdminClientAsync(_factory);
        var suffix = Guid.NewGuid().ToString("N")[..5].ToUpper();

        // Group + client with PAN linked to that group.
        var groupResp = await c.PostAsJsonAsync("/api/v1/masters/client-groups",
            new ClientGroupUpsertDto($"GRP-{suffix}", $"Family {suffix}", null));
        var group = (await groupResp.Content.ReadFromJsonAsync<ClientGroupDto>())!;
        var etResp = await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto($"ET-{suffix}", $"ET {suffix}", null));
        var et = (await etResp.Content.ReadFromJsonAsync<EntityTypeDto>())!;
        var svcResp = await c.PostAsJsonAsync("/api/v1/masters/services",
            new ServiceUpsertDto($"SV-{suffix}", $"Svc {suffix}", null, null, 0));
        var svc = (await svcResp.Content.ReadFromJsonAsync<ServiceDto>())!;

        var cliResp = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            LegalName: $"Rich DTO Co {suffix}",
            EntityTypeId: et.Id, TradeName: null, Pan: "ABCDE1234F",
            ClientGroupId: group.Id, LegacyClientId: null, AcknowledgeDuplicatePan: true));
        var client = (await cliResp.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        var fileResp = await c.PostAsJsonAsync($"/api/v1/clients/{client.Id}/service-files",
            new ClientServiceFilesController.CreateServiceFileRequest(
                svc.Id, null, new DateOnly(2024, 4, 1), null, null));
        fileResp.EnsureSuccessStatusCode();
        var dto = (await fileResp.Content.ReadFromJsonAsync<ClientServiceFilesController.ServiceFileDto>())!;

        dto.ClientPan.Should().Be("ABCDE1234F");
        dto.ClientGroupName.Should().Be($"Family {suffix}");
        dto.ClientGroupCode.Should().Be($"GRP-{suffix}");
    }
}
