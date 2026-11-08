using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OnlineShop.Application.Accounts;
using OnlineShop.IntegrationTests.TestSupport;

namespace OnlineShop.IntegrationTests;

/// <summary>Two-step sign-in end to end, with codes computed like an authenticator app would.</summary>
public sealed class TwoFactorTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse 42";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Registers a user and turns on two-step sign-in; returns (email, shared key, recovery codes).</summary>
    private static async Task<(string Email, string Key, IReadOnlyList<string> RecoveryCodes)> EnrolAsync(HttpClient client)
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/api/v1/account/register", new RegisterRequest(email, Password, null, "en"))).EnsureSuccessStatusCode();

        using var setupResponse = await client.PostAsync("/api/v1/account/2fa/setup", null);
        setupResponse.EnsureSuccessStatusCode();
        var setup = (await setupResponse.Content.ReadFromJsonAsync<AuthenticatorSetup>(Json))!;
        Assert.StartsWith("otpauth://totp/", setup.AuthenticatorUri, StringComparison.Ordinal);

        using var enable = await client.PostAsJsonAsync("/api/v1/account/2fa/enable", new TwoFactorCodeRequest(Totp.Code(setup.SharedKey)));
        enable.EnsureSuccessStatusCode();
        var codes = (await enable.Content.ReadFromJsonAsync<RecoveryCodes>(Json))!.Codes;
        return (email, setup.SharedKey, codes);
    }

    [Fact]
    public async Task Enabling_needs_a_correct_first_code_and_returns_ten_recovery_codes()
    {
        using var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/account/register",
            new RegisterRequest($"user-{Guid.NewGuid():N}@example.com", Password, null, "en"))).EnsureSuccessStatusCode();
        await client.PostAsync("/api/v1/account/2fa/setup", null);

        using var wrong = await client.PostAsJsonAsync("/api/v1/account/2fa/enable", new TwoFactorCodeRequest("000000"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        using var otherClient = factory.CreateClient();
        var (_, _, codes) = await EnrolAsync(otherClient);
        Assert.Equal(10, codes.Count);
        var status = await otherClient.GetFromJsonAsync<TwoFactorStatus>("/api/v1/account/2fa", Json);
        Assert.Equal((true, 10), (status!.Enabled, status.RecoveryCodesLeft));
        Assert.True((await otherClient.GetFromJsonAsync<CurrentUser>("/api/v1/account/me", Json))!.TwoFactorEnabled);
    }

    [Fact]
    public async Task A_password_alone_no_longer_signs_in_the_authenticator_completes_it()
    {
        using var enrolClient = factory.CreateClient();
        var (email, key, _) = await EnrolAsync(enrolClient);

        using var client = factory.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, false));
        await AssertProblemAsync(login, HttpStatusCode.Forbidden, AccountErrors.TwoFactorRequired);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/me")).StatusCode);

        using var wrong = await client.PostAsJsonAsync("/api/v1/account/2fa/verify", new TwoFactorSignInRequest("000000", false, false));
        await AssertProblemAsync(wrong, HttpStatusCode.BadRequest, AccountErrors.CodeInvalid);

        using var right = await client.PostAsJsonAsync("/api/v1/account/2fa/verify", new TwoFactorSignInRequest(Totp.Code(key), false, false));
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Equal(email, (await client.GetFromJsonAsync<CurrentUser>("/api/v1/account/me", Json))!.Email);
    }

    [Fact]
    public async Task A_recovery_code_signs_in_once()
    {
        using var enrolClient = factory.CreateClient();
        var (email, _, codes) = await EnrolAsync(enrolClient);

        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, false));
        using var first = await client.PostAsJsonAsync("/api/v1/account/2fa/recovery", new RecoveryCodeSignInRequest(codes[0]));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        await client.PostAsync("/api/v1/account/logout", null);
        await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, false));
        using var reuse = await client.PostAsJsonAsync("/api/v1/account/2fa/recovery", new RecoveryCodeSignInRequest(codes[0]));
        await AssertProblemAsync(reuse, HttpStatusCode.BadRequest, AccountErrors.CodeInvalid);

        var status = await enrolClient.GetFromJsonAsync<TwoFactorStatus>("/api/v1/account/2fa", Json);
        Assert.Equal(9, status!.RecoveryCodesLeft);
    }

    [Fact]
    public async Task A_remembered_device_skips_the_second_step_until_forgotten()
    {
        using var enrolClient = factory.CreateClient();
        var (email, key, _) = await EnrolAsync(enrolClient);

        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, false));
        (await client.PostAsJsonAsync("/api/v1/account/2fa/verify", new TwoFactorSignInRequest(Totp.Code(key), false, RememberDevice: true))).EnsureSuccessStatusCode();
        await client.PostAsync("/api/v1/account/logout", null);

        using var again = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, false));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        (await client.PostAsync("/api/v1/account/2fa/forget-device", null)).EnsureSuccessStatusCode();
        await client.PostAsync("/api/v1/account/logout", null);
        using var afterForget = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, false));
        await AssertProblemAsync(afterForget, HttpStatusCode.Forbidden, AccountErrors.TwoFactorRequired);
    }

    [Fact]
    public async Task The_second_step_without_a_first_step_must_start_over()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/account/2fa/verify", new TwoFactorSignInRequest("123456", false, false));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, AccountErrors.TwoFactorSessionExpired);
    }

    [Fact]
    public async Task Turning_it_off_needs_a_current_code_and_restores_password_only_sign_in()
    {
        using var client = factory.CreateClient();
        var (email, key, _) = await EnrolAsync(client);

        using var wrong = await client.PostAsJsonAsync("/api/v1/account/2fa/disable", new TwoFactorCodeRequest("000000"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        using var right = await client.PostAsJsonAsync("/api/v1/account/2fa/disable", new TwoFactorCodeRequest(Totp.Code(key)));
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);

        using var other = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await other.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, false))).StatusCode);
    }

    [Fact]
    public async Task Managing_two_step_sign_in_needs_a_session()
    {
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/2fa")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/account/2fa/setup", null)).StatusCode);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
