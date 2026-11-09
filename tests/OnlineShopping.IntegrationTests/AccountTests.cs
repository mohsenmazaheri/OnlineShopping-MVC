using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OnlineShopping.Application.Accounts;
using OnlineShopping.Application.Authorization;
using OnlineShopping.Infrastructure;
using OnlineShopping.IntegrationTests.TestSupport;

namespace OnlineShopping.IntegrationTests;

public sealed class AccountTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse 42";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Register_signs_in_with_an_HttpOnly_session_cookie_and_me_returns_the_customer()
    {
        using var client = factory.CreateClient();
        var email = NewEmail();

        using var register = await client.PostAsJsonAsync("/api/v1/account/register", new RegisterRequest(email, Password, "Ali", "en"));

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var cookie = Assert.Single(register.Headers.GetValues("Set-Cookie"), c => c.StartsWith(DependencyInjection.AuthCookieName + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);

        var me = await client.GetFromJsonAsync<CurrentUser>("/api/v1/account/me", Json);
        Assert.Equal(email, me!.Email);
        Assert.Equal("Ali", me.DisplayName);
        Assert.False(me.EmailConfirmed);
        Assert.Equal([AppRoles.Customer], me.Roles);
        Assert.Empty(me.Permissions);
    }

    [Fact]
    public async Task Me_requires_a_session_and_logout_ends_it()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/me")).StatusCode);

        await RegisterAsync(client, NewEmail());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/account/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/me")).StatusCode);
    }

    [Fact]
    public async Task The_confirmation_email_link_confirms_the_address()
    {
        using var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var mail = factory.Emails.Sent.Last(m => m.To == email);
        Assert.Contains(ApiFactory.PublicUrl + "/en/account/confirm-email?", mail.TextBody, StringComparison.Ordinal);
        var (userId, token) = factory.Emails.LinkParametersFor(email);

        using var confirm = await client.PostAsJsonAsync("/api/v1/account/confirm-email", new ConfirmEmailRequest(userId, token));

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.True((await client.GetFromJsonAsync<CurrentUser>("/api/v1/account/me", Json))!.EmailConfirmed);
    }

    [Fact]
    public async Task A_forged_confirmation_token_is_rejected()
    {
        using var client = factory.CreateClient();
        var user = await RegisterAsync(client, NewEmail());

        using var response = await client.PostAsJsonAsync("/api/v1/account/confirm-email", new ConfirmEmailRequest(user.Id, "forged"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, AccountErrors.InvalidToken);
    }

    [Fact]
    public async Task Registering_twice_with_the_same_email_is_a_conflict()
    {
        using var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        using var again = await factory.CreateClient().PostAsJsonAsync("/api/v1/account/register",
            new RegisterRequest(email.ToUpperInvariant(), Password, null, null));

        await AssertProblemAsync(again, HttpStatusCode.Conflict, AccountErrors.EmailTaken);
    }

    [Fact]
    public async Task An_eight_character_password_with_a_digit_is_accepted()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/account/register", new RegisterRequest(NewEmail(), "abcdefg1", null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Weak_passwords_get_a_validation_problem_with_error_codes()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/account/register", new RegisterRequest(NewEmail(), "short1", null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var codes = body.RootElement.GetProperty("errors").GetProperty("password").EnumerateArray().Select(e => e.GetString());
        Assert.Contains(AccountErrors.PasswordTooShort, codes);
    }

    [Fact]
    public async Task Sign_in_codes_are_unavailable_while_no_SMS_provider_is_configured()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new SendPhoneCodeRequest("09121234567", "fa"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, AccountErrors.SmsDisabled);
    }

    [Fact]
    public async Task Login_with_the_right_password_works_and_wrong_ones_all_look_the_same()
    {
        var email = NewEmail();
        await RegisterAsync(factory.CreateClient(), email);
        using var client = factory.CreateClient();

        await AssertProblemAsync(await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, "wrong password 1", false)),
            HttpStatusCode.BadRequest, AccountErrors.InvalidCredentials);
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(NewEmail(), Password, false)),
            HttpStatusCode.BadRequest, AccountErrors.InvalidCredentials);

        using var ok = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, true));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(email, (await client.GetFromJsonAsync<CurrentUser>("/api/v1/account/me", Json))!.Email);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_password()
    {
        var email = NewEmail();
        await RegisterAsync(factory.CreateClient(), email);
        using var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, $"wrong password {i}", false));

        using var locked = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, false));
        await AssertProblemAsync(locked, HttpStatusCode.Forbidden, AccountErrors.LockedOut);
    }

    [Fact]
    public async Task Password_reset_by_email_replaces_the_password()
    {
        var email = NewEmail();
        await RegisterAsync(factory.CreateClient(), email);
        using var client = factory.CreateClient();

        using var forgot = await client.PostAsJsonAsync("/api/v1/account/forgot-password", new ForgotPasswordRequest(email, "fa"));
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        Assert.Contains(ApiFactory.PublicUrl + "/en/account/reset-password?", factory.Emails.Sent.Last(m => m.To == email).TextBody, StringComparison.Ordinal);
        var (userId, token) = factory.Emails.LinkParametersFor(email);

        const string newPassword = "a brand new passphrase 9";
        using var reset = await client.PostAsJsonAsync("/api/v1/account/reset-password", new ResetPasswordRequest(userId, token, newPassword));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, false))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, newPassword, false))).StatusCode);

        // The link works only once.
        using var reuse = await client.PostAsJsonAsync("/api/v1/account/reset-password", new ResetPasswordRequest(userId, token, "yet another pass 11"));
        await AssertProblemAsync(reuse, HttpStatusCode.BadRequest, AccountErrors.InvalidToken);
    }

    [Fact]
    public async Task Forgot_password_gives_the_same_answer_for_unknown_emails_and_sends_nothing()
    {
        using var client = factory.CreateClient();
        var email = NewEmail();

        using var response = await client.PostAsJsonAsync("/api/v1/account/forgot-password", new ForgotPasswordRequest(email, null));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain(factory.Emails.Sent, m => m.To == email);
    }

    [Fact]
    public async Task State_changing_requests_from_another_site_are_rejected()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/login")
        {
            Content = JsonContent.Create(new LoginRequest(NewEmail(), Password, false))
        };
        request.Headers.Add("Origin", "https://evil.example");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Requests_from_the_storefront_origin_are_accepted()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/register")
        {
            Content = JsonContent.Create(new RegisterRequest(NewEmail(), Password, null, null))
        };
        request.Headers.Add("Origin", ApiFactory.PublicUrl);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<CurrentUser> RegisterAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/account/register", new RegisterRequest(email, Password, null, "en"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CurrentUser>(Json))!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
