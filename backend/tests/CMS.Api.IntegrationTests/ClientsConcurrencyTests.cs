using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using CMS.Api.Controllers.Masters;
using CMS.Application.Abstractions;
using CMS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class ClientsConcurrencyTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public ClientsConcurrencyTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public async Task Code_Generator_Produces_Unique_Monotonic_Codes_Across_Scopes()
    {
        // Each call creates a FRESH scope — which in production (Postgres) would use its own pooled
        // connection and serialise on the row lock. On SQLite+shared-connection we drive them serially,
        // which exercises the same code path (transaction + atomic UPDATE + read-back) and proves
        // uniqueness and monotonicity. The row lock guarantee itself is a Postgres property.
        const int N = 25;
        var codes = new List<string>(N);
        for (int i = 0; i < N; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var gen = scope.ServiceProvider.GetRequiredService<IClientCodeGenerator>();
            codes.Add(await gen.NextAsync());
        }
        codes.Should().OnlyHaveUniqueItems();
        codes.All(x => x.Length == 8 && x.All(char.IsDigit)).Should().BeTrue();

        // Numeric monotonicity — the generator yields strictly increasing values; code holes
        // (from aborted inserts) are permanent and never reused.
        var asLongs = codes.Select(long.Parse).ToArray();
        for (int i = 1; i < asLongs.Length; i++)
            asLongs[i].Should().BeGreaterThan(asLongs[i - 1]);
    }

    [Fact]
    public async Task Update_With_Stale_Version_Returns_409()
    {
        var c = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var et = await c.PostAsJsonAsync("/api/v1/masters/entity-types",
            new EntityTypeUpsertDto($"CONC{Guid.NewGuid().ToString("N")[..4].ToUpper()}", "Concurrency ET", null));
        var etDto = (await et.Content.ReadFromJsonAsync<EntityTypeDto>())!;

        var create = await c.PostAsJsonAsync("/api/v1/clients", new ClientsController.CreateClientRequest(
            "Concurrency Co", etDto.Id, null, null, null, null));
        var dto = (await create.Content.ReadFromJsonAsync<ClientsController.ClientDto>())!;

        // Fresh write — succeeds and bumps the stored version to 2.
        var ok = await c.PutAsJsonAsync($"/api/v1/clients/{dto.Id}", new ClientsController.UpdateClientRequest(
            "Fresh update", etDto.Id, null, null, null, null, dto.Version));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second write with the ORIGINAL version — must be rejected.
        var stale = await c.PutAsJsonAsync($"/api/v1/clients/{dto.Id}", new ClientsController.UpdateClientRequest(
            "Stale update", etDto.Id, null, null, null, null, dto.Version));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
