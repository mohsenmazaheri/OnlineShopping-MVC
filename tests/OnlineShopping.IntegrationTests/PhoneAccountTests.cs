using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineShopping.Api.Endpoints;
using OnlineShopping.Application.Accounts;
using OnlineShopping.Application.Authorization;
using OnlineShopping.Application.Notifications;
using Microsoft.Extensions.Time.Testing;
using OnlineShopping.IntegrationTests.TestSupport;

namespace OnlineShopping.IntegrationTests;

/// <summary>Sign-in with SMS codes against PostgreSQL, with an SMS provider configured and messages captured.</summary>
public sealed class PhoneAccountTests(PhoneAccountTests.SmsFactory factory) : IClassFixture<PhoneAccountTests.SmsFactory>
{
    public sealed class SmsFactory : ApiFactory
    {
        public CapturingSms Sms { get; } = new();

        /// <summary>Controllable clock, so cooldowns can be passed without real waiting.</summary>
        public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

        protected override IReadOnlyDictionary<string, string?> ConfigurationOverrides => new Dictionary<string, string?>
        {
            ["Sms:Provider"] = "Pickup",
            // Longer than the 30-minute session refresh, so a test can show the sign-in time survives it.
            ["Auth:ReauthenticationMinutes"] = "60"
        };

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISmsSender>();
                services.AddSingleton<ISmsSender>(Sms);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
            });
        }
    }

    public sealed class CapturingSms : ISmsSender
    {
        private readonly ConcurrentDictionary<string, string> _lastCode = new();

        public string LastCodeFor(string phoneE164) => _lastCode[phoneE164];

        public Task SendVerificationCodeAsync(string phoneE164, string code, string locale, CancellationToken cancellationToken = default)
        {
            _lastCode[phoneE164] = code;
            return Task.CompletedTask;
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>A random valid Iranian mobile number: (local form, E.164).</summary>
    private static (string Local, string E164) NewPhone()
    {
        var digits = RandomNumberGenerator.GetInt32(0, 10_000_000).ToString("D7", System.Globalization.CultureInfo.InvariantCulture);
        return ($"0912{digits}", $"+98912{digits}");
    }

    [Fact]
    public async Task Store_info_offers_code_sign_in_when_SMS_is_configured()
    {
        using var client = factory.CreateClient();

        var store = await client.GetFromJsonAsync<StoreInfoResponse>("/api/v1/store", Json);

        Assert.True(store!.SignIn.Password);
        Assert.True(store.SignIn.PhoneCode);
        Assert.Empty(store.SignIn.External);
        Assert.True(store.SignUp.Phone);
    }

    [Fact]
    public async Task A_new_number_gets_a_verified_account_and_a_session_with_the_code()
    {
        using var client = factory.CreateClient();
        var (local, e164) = NewPhone();

        using var send = await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new SendPhoneCodeRequest(local, "fa"));
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        Assert.Equal(new PhoneCodeSent(180, 60), await send.Content.ReadFromJsonAsync<PhoneCodeSent>(Json));

        using var signIn = await client.PostAsJsonAsync("/api/v1/account/phone/sign-in",
            new PhoneSignInRequest(local, factory.Sms.LastCodeFor(e164), "سارا", "fa", false));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var result = await signIn.Content.ReadFromJsonAsync<PhoneSignInResult>(Json);
        Assert.True(result!.IsNewAccount);

        var me = await client.GetFromJsonAsync<CurrentUser>("/api/v1/account/me", Json);
        Assert.Equal(e164, me!.PhoneNumber);
        Assert.True(me.PhoneNumberConfirmed);
        Assert.Null(me.Email);
        Assert.Equal("سارا", me.DisplayName);
        Assert.Equal([AppRoles.Customer], me.Roles);
    }

    [Fact]
    public async Task The_same_number_signs_in_to_the_same_account_next_time()
    {
        var (local, e164) = NewPhone();
        var first = await SignInWithCodeAsync(factory.CreateClient(), local, e164);
        factory.Clock.Advance(TimeSpan.FromSeconds(61)); // past the resend cooldown

        using var client = factory.CreateClient();
        var second = await SignInWithCodeAsync(client, local, e164);

        Assert.False(second.IsNewAccount);
        Assert.Equal(first.User.Id, second.User.Id);
    }

    [Fact]
    public async Task A_wrong_code_is_rejected_and_creates_nothing()
    {
        using var client = factory.CreateClient();
        var (local, e164) = NewPhone();
        await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new SendPhoneCodeRequest(local, "fa"));
        var wrong = factory.Sms.LastCodeFor(e164) == "000000" ? "111111" : "000000";

        using var response = await client.PostAsJsonAsync("/api/v1/account/phone/sign-in", new PhoneSignInRequest(local, wrong, null, null, false));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, AccountErrors.CodeInvalid);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/me")).StatusCode);
    }

    [Fact]
    public async Task Asking_again_within_the_cooldown_gets_429_with_a_code()
    {
        using var client = factory.CreateClient();
        var (local, _) = NewPhone();
        await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new SendPhoneCodeRequest(local, "fa"));

        using var again = await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new SendPhoneCodeRequest(local, "fa"));

        await AssertProblemAsync(again, HttpStatusCode.TooManyRequests, AccountErrors.CodeCooldown);
    }

    [Fact]
    public async Task An_email_account_can_add_a_verified_phone()
    {
        using var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/api/v1/account/register", new RegisterRequest(email, "correct horse 42", null, "en"))).EnsureSuccessStatusCode();
        var (local, e164) = NewPhone();

        (await client.PostAsJsonAsync("/api/v1/account/phone/add/send-code", new SendPhoneCodeRequest(local, null))).EnsureSuccessStatusCode();
        using var confirm = await client.PostAsJsonAsync("/api/v1/account/phone/add/confirm", new ConfirmPhoneRequest(local, factory.Sms.LastCodeFor(e164)));

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var me = await client.GetFromJsonAsync<CurrentUser>("/api/v1/account/me", Json);
        Assert.Equal((email, e164, true), (me!.Email, me.PhoneNumber, me.PhoneNumberConfirmed));
    }

    [Fact]
    public async Task Adding_a_phone_requires_a_session()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/account/phone/add/send-code", new SendPhoneCodeRequest(NewPhone().Local, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_number_owned_by_another_account_cannot_be_added()
    {
        var (local, e164) = NewPhone();
        await SignInWithCodeAsync(factory.CreateClient(), local, e164);

        using var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/account/register",
            new RegisterRequest($"user-{Guid.NewGuid():N}@example.com", "correct horse 42", null, null))).EnsureSuccessStatusCode();

        using var response = await client.PostAsJsonAsync("/api/v1/account/phone/add/send-code", new SendPhoneCodeRequest(local, null));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, AccountErrors.PhoneTaken);
    }

    [Fact]
    public async Task A_password_less_account_deletes_itself_soon_after_signing_in_even_across_a_session_refresh()
    {
        var (local, e164) = NewPhone();
        using var client = factory.CreateClient();
        var first = await SignInWithCodeAsync(client, local, e164);

        // Past the 30-minute security stamp check: the session is rebuilt from the database and must keep auth_time.
        factory.Clock.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/me")).StatusCode);

        using (var deleted = await client.PostAsJsonAsync("/api/v1/account/delete", new DeleteAccountRequest(null)))
            Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/me")).StatusCode);

        // The number is free again: signing in with it creates a new, empty account.
        factory.Clock.Advance(TimeSpan.FromSeconds(61));
        using var fresh = factory.CreateClient();
        var again = await SignInWithCodeAsync(fresh, local, e164);
        Assert.True(again.IsNewAccount);
        Assert.NotEqual(first.User.Id, again.User.Id);
    }

    [Fact]
    public async Task Long_after_signing_in_a_password_less_account_must_sign_in_again_to_delete_itself()
    {
        var (local, e164) = NewPhone();
        using var client = factory.CreateClient();
        await SignInWithCodeAsync(client, local, e164);
        factory.Clock.Advance(TimeSpan.FromMinutes(61));

        using (var refused = await client.PostAsJsonAsync("/api/v1/account/delete", new DeleteAccountRequest(null)))
            await AssertProblemAsync(refused, HttpStatusCode.Forbidden, ProfileErrors.ReauthenticationRequired);

        await SignInWithCodeAsync(client, local, e164);
        using var deleted = await client.PostAsJsonAsync("/api/v1/account/delete", new DeleteAccountRequest(null));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
    }

    private async Task<PhoneSignInResult> SignInWithCodeAsync(HttpClient client, string local, string e164)
    {
        using (var send = await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new SendPhoneCodeRequest(local, "fa")))
            send.EnsureSuccessStatusCode();

        using var signIn = await client.PostAsJsonAsync("/api/v1/account/phone/sign-in",
            new PhoneSignInRequest(local, factory.Sms.LastCodeFor(e164), null, "fa", false));
        signIn.EnsureSuccessStatusCode();
        return (await signIn.Content.ReadFromJsonAsync<PhoneSignInResult>(Json))!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
