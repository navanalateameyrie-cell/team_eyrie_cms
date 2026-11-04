using System.Net;
using System.Reflection;
using CMS.Domain.Auditing;
using FluentAssertions;
using Xunit;

namespace CMS.Api.IntegrationTests;

/// <summary>
/// Audit records must stay non-editable. Enforcement is two-pronged:
///   1. Code: the <see cref="AuditEntry"/> type has no public setters and no public mutation methods.
///   2. API: the controller exposes only GETs — any write verb at /api/v1/audit/{id} returns 405/404.
/// </summary>
public class AuditImmutabilityTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public AuditImmutabilityTests(CmsApiFactory f) => _factory = f;

    [Fact]
    public void AuditEntry_Type_Has_No_Public_Setters_And_No_Public_Mutation_Methods()
    {
        var t = typeof(AuditEntry);

        // No public setters on any property.
        var publicWritableProps = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && (p.SetMethod?.IsPublic ?? false))
            .ToList();
        publicWritableProps.Should().BeEmpty(
            "audit rows are append-only; a public setter would open an edit path");

        // Only read-only public API: factory statics + object inherited methods. No public instance
        // methods that could mutate state (property getters are filtered out — those are reads).
        var publicInstanceMutators = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)  // excludes get_X / set_X / add_X / remove_X
            .ToList();
        publicInstanceMutators.Should().BeEmpty(
            "audit rows must not expose public instance methods that could mutate state");
    }

    [Fact]
    public async Task PUT_PATCH_DELETE_On_Audit_Endpoint_Are_Not_Routed()
    {
        var c = _factory.CreateClient();
        var token = await _factory.LoginAsAdminAsync(c);
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var someId = Guid.NewGuid();
        foreach (var verb in new[] { HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch })
        {
            var req = new HttpRequestMessage(verb, $"/api/v1/audit/{someId}");
            var resp = await c.SendAsync(req);
            resp.StatusCode.Should().Match(s =>
                s == HttpStatusCode.MethodNotAllowed || s == HttpStatusCode.NotFound,
                $"{verb} on an audit record must not succeed");
            resp.IsSuccessStatusCode.Should().BeFalse();
        }
    }
}
