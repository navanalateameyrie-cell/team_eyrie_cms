using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.Api.Controllers;
using FluentAssertions;
using OtpNet;
using Xunit;

namespace CMS.Api.IntegrationTests;

public class MfaTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;
    public MfaTests(CmsApiFactory f) => _factory = f;

    private static string NowCode(string base32Secret)
    {
        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        return totp.ComputeTotp();
    }

    private static async Task<(HttpClient http, string accessToken)> AdminAsync(CmsApiFactory f)
    {
        var http = f.CreateClient();
        var login = await http.PostAsJsonAsync("/api/v1/auth/login",
            new AuthController.LoginRequest(CmsApiFactory.DevAdminEmail, CmsApiFactory.DevAdminPassword));
        var body = (await login.Content.ReadFromJsonAsync<AuthController.LoginResponse>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        return (http, body.AccessToken);
    }

    [Fact]
    public async Task Enrol_Roundtrip_Transitions_IsEnrolled_To_True()
    {
        var email = $"enrol-{Guid.NewGuid():N}@tests.cms.local";
        var token = await _factory.CreateUserAndLoginAsync(email, "StaffUser");
        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var start = await http.PostAsJsonAsync("/api/v1/auth/mfa/enrol-start", new {});
        start.EnsureSuccessStatusCode();
        var enrol = (await start.Content.ReadFromJsonAsync<AuthController.MfaEnrolStartResponse>())!;
        enrol.Base32Secret.Should().NotBeNullOrWhiteSpace();
        enrol.OtpAuthUri.Should().StartWith("otpauth://totp/");

        var verify = await http.PostAsJsonAsync("/api/v1/auth/mfa/enrol-verify",
            new AuthController.MfaEnrolVerifyRequest(NowCode(enrol.Base32Secret)));
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await http.GetFromJsonAsync<AuthController.MfaStatusResponse>("/api/v1/auth/mfa/status");
        status!.IsEnrolled.Should().BeTrue();
        status.EnrolledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Enrol_Wrong_Code_Is_Rejected_422()
    {
        // Use a fresh user — tests share the factory's SQLite instance, so another test's MFA enrol
        // on the admin would turn this login into an MFA-pending flow instead.
        var email = $"wrongcode-{Guid.NewGuid():N}@tests.cms.local";
        var token = await _factory.CreateUserAndLoginAsync(email, "StaffUser");
        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await http.PostAsJsonAsync("/api/v1/auth/mfa/enrol-start", new {})).EnsureSuccessStatusCode();
        var r = await http.PostAsJsonAsync("/api/v1/auth/mfa/enrol-verify",
            new AuthController.MfaEnrolVerifyRequest("000000"));
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_After_Enrol_Requires_Challenge_And_Fresh_Code_Yields_Tokens()
    {
        // Create a dedicated user so we don't collide with other tests that assume the admin has no MFA.
        var email = $"mfa-{Guid.NewGuid():N}@tests.cms.local";
        var token = await _factory.CreateUserAndLoginAsync(email, "SystemAdmin");
        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var enrol = (await (await http.PostAsJsonAsync("/api/v1/auth/mfa/enrol-start", new {}))
            .Content.ReadFromJsonAsync<AuthController.MfaEnrolStartResponse>())!;
        (await http.PostAsJsonAsync("/api/v1/auth/mfa/enrol-verify",
            new AuthController.MfaEnrolVerifyRequest(NowCode(enrol.Base32Secret)))).EnsureSuccessStatusCode();

        var fresh = _factory.CreateClient();
        var loginResp = await fresh.PostAsJsonAsync("/api/v1/auth/login",
            new AuthController.LoginRequest(email, "Str0ng!Password"));
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var mfaRequired = (await loginResp.Content.ReadFromJsonAsync<AuthController.MfaChallengeRequiredResponse>())!;
        mfaRequired.MfaRequired.Should().BeTrue();
        mfaRequired.MfaPendingToken.Should().NotBeNullOrWhiteSpace();

        // Wrong code first.
        var bad = await fresh.PostAsJsonAsync("/api/v1/auth/mfa/challenge",
            new AuthController.MfaChallengeRequest(mfaRequired.MfaPendingToken, "000000"));
        bad.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Correct code produces the full LoginResponse (access + refresh).
        var ok = await fresh.PostAsJsonAsync("/api/v1/auth/mfa/challenge",
            new AuthController.MfaChallengeRequest(mfaRequired.MfaPendingToken, NowCode(enrol.Base32Secret)));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var full = (await ok.Content.ReadFromJsonAsync<AuthController.LoginResponse>())!;
        full.AccessToken.Should().NotBeNullOrEmpty();
        full.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_For_User_Without_Mfa_Returns_Tokens_Directly()
    {
        var email = $"nomfa-{Guid.NewGuid():N}@tests.cms.local";
        await _factory.CreateUserAndLoginAsync(email, "StaffUser");

        var fresh = _factory.CreateClient();
        var r = await fresh.PostAsJsonAsync("/api/v1/auth/login",
            new AuthController.LoginRequest(email, "Str0ng!Password"));
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadAsStringAsync();
        body.Should().NotContain("mfaRequired");
    }
}
