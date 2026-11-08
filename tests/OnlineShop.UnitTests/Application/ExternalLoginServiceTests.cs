using Microsoft.Extensions.Options;
using OnlineShop.Application.Accounts;
using OnlineShop.Application.Common;
using OnlineShop.Infrastructure.Identity;
using OnlineShop.UnitTests.TestSupport;

namespace OnlineShop.UnitTests.Application;

public sealed class ExternalLoginServiceTests : IDisposable
{
    private const string Password = "correct horse 42";

    private readonly FakeAccountIdentity _identity = new();
    private readonly CapturingEmailSender _email = new();
    private readonly SqliteTestDatabase _database = new();
    private readonly List<IDisposable> _contexts = [];

    private sealed class FixedProviders(params string[] enabled) : IExternalLoginProviders
    {
        public IReadOnlyList<string> Enabled { get; } = enabled;
    }

    public void Dispose()
    {
        _contexts.ForEach(c => c.Dispose());
        _database.Dispose();
    }

    private ExternalLoginService Create(bool smsEnabled = false, AuthOptions? auth = null, params string[] enabled)
    {
        var store = Options.Create(new StoreOptions { Name = "S", DefaultLocale = "fa", Locales = "fa,en", PublicUrl = "https://shop.example.com" });
        var authOptions = Options.Create(auth ?? new AuthOptions());
        var sms = new FixedSmsCapability(smsEnabled);
        var db = _database.CreateContext();
        _contexts.Add(db);
        var normalizer = new PhoneNumberNormalizer();
        var accounts = new AccountService(_identity, new LoginIdentifierParser(normalizer), normalizer,
            new PhoneCodeService(db, new CapturingSmsSender(), sms, Options.Create(new PhoneCodeOptions()), _database.Clock),
            _email, store, authOptions);
        var providers = enabled.Length == 0 ? new FixedProviders(ExternalProviders.All.ToArray()) : new FixedProviders(enabled);
        return new ExternalLoginService(_identity, providers, accounts, sms, authOptions);
    }

    private static ExternalLoginInfo Google(string email = "ali@example.com", bool verified = true, string key = "g-1") =>
        new(ExternalProviders.Google, key, email, verified, "Ali");

    [Fact]
    public async Task A_new_person_gets_an_account_with_the_login_attached_and_is_signed_in()
    {
        var result = await Create().SignInAsync(Google(), "en");

        Assert.True(result.Value.IsNewAccount);
        var user = Assert.Single(_identity.Users);
        Assert.True(user.EmailConfirmed); // Google verified the address
        Assert.Equal((user.Id, "google", "g-1"), Assert.Single(_identity.Logins));
        Assert.Equal(user.Id, _identity.SignedInUserId);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task An_unverified_email_creates_an_unconfirmed_account_and_sends_a_confirmation()
    {
        var info = new ExternalLoginInfo(ExternalProviders.Microsoft, "m-1", "sara@example.com", false, null);

        var result = await Create().SignInAsync(info, "en");

        Assert.True(result.IsSuccess);
        Assert.False(_identity.Users.Single().EmailConfirmed);
        Assert.Equal("sara@example.com", _email.Sent.Single().To);
    }

    [Fact]
    public async Task A_returning_person_signs_in_to_the_linked_account()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "ali@example.com", null, Password, "fa"));
        _identity.Logins.Add(("u0", "google", "g-1"));

        var result = await Create().SignInAsync(Google(email: "changed@example.com"), "fa");

        Assert.False(result.Value.IsNewAccount);
        Assert.Equal("u0", _identity.SignedInUserId);
    }

    [Fact]
    public async Task A_verified_email_is_attached_to_the_existing_account()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "ali@example.com", null, Password, "fa"));

        var result = await Create().SignInAsync(Google(email: "ALI@example.com"), "fa");

        Assert.False(result.Value.IsNewAccount);
        Assert.Equal(("u0", "google", "g-1"), Assert.Single(_identity.Logins));
        Assert.Single(_identity.Users);
    }

    [Fact]
    public async Task An_unverified_email_never_takes_over_an_existing_account()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "ali@example.com", null, Password, "fa"));
        var attacker = new ExternalLoginInfo(ExternalProviders.Facebook, "fb-evil", "ali@example.com", false, "Not Ali");

        var result = await Create().SignInAsync(attacker, "fa");

        Assert.Equal(AccountErrors.ExternalEmailInUse, result.Error!.Code);
        Assert.Empty(_identity.Logins);
        Assert.Null(_identity.SignedInUserId);
    }

    [Fact]
    public async Task A_provider_without_an_email_cannot_create_an_account()
    {
        var result = await Create().SignInAsync(new ExternalLoginInfo(ExternalProviders.Facebook, "fb-1", null, false, "X"), "fa");

        Assert.Equal(AccountErrors.ExternalNoEmail, result.Error!.Code);
        Assert.Empty(_identity.Users);
    }

    [Fact]
    public async Task Disabled_providers_are_refused()
    {
        var result = await Create(enabled: ExternalProviders.Microsoft).SignInAsync(Google(), "fa");

        Assert.Equal(AccountErrors.ExternalProviderDisabled, result.Error!.Code);
    }

    [Fact]
    public async Task New_accounts_respect_the_email_sign_up_setting()
    {
        var result = await Create(auth: new AuthOptions { EmailSignUp = false }).SignInAsync(Google(), "fa");

        Assert.Equal(AccountErrors.EmailSignUpDisabled, result.Error!.Code);
    }

    [Fact]
    public async Task A_locked_account_stays_locked()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "ali@example.com", null, Password, "fa"));
        _identity.Logins.Add(("u0", "google", "g-1"));
        _identity.LockedOut = true;

        var result = await Create().SignInAsync(Google(), "fa");

        Assert.Equal(AccountErrors.LockedOut, result.Error!.Code);
        Assert.Null(_identity.SignedInUserId);
    }

    [Fact]
    public async Task Linking_attaches_the_provider_and_refuses_one_owned_by_another_account()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "a@x.io", null, Password, "fa"));
        _identity.Users.Add(new FakeAccountIdentity.User("u1", "b@x.io", null, Password, "fa"));
        _identity.Logins.Add(("u1", "google", "g-taken"));
        var service = Create();

        Assert.True((await service.LinkAsync("u0", Google(key: "g-mine"))).IsSuccess);
        Assert.True((await service.LinkAsync("u0", Google(key: "g-mine"))).IsSuccess); // idempotent
        Assert.Equal(AccountErrors.ExternalLoginInUse, (await service.LinkAsync("u0", Google(key: "g-taken"))).Error!.Code);
    }

    [Fact]
    public async Task The_only_way_to_sign_in_cannot_be_unlinked()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "a@x.io", null, "", "fa") { CurrentPassword = null });
        _identity.Logins.Add(("u0", "google", "g-1"));

        var result = await Create().UnlinkAsync("u0", ExternalProviders.Google);

        Assert.Equal(AccountErrors.ExternalLastMethod, result.Error!.Code);
        Assert.Single(_identity.Logins);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("phone")]
    [InlineData("second-provider")]
    public async Task Unlinking_works_when_another_sign_in_method_remains(string other)
    {
        var user = new FakeAccountIdentity.User("u0", "a@x.io", other == "phone" ? "+989121234567" : null, "", "fa")
        {
            CurrentPassword = other == "password" ? Password : null
        };
        _identity.Users.Add(user);
        _identity.Logins.Add(("u0", "google", "g-1"));
        if (other == "second-provider")
            _identity.Logins.Add(("u0", "microsoft", "m-1"));

        var result = await Create(smsEnabled: true).UnlinkAsync("u0", ExternalProviders.Google);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("google", result.Value.Linked);
    }

    [Fact]
    public async Task A_confirmed_phone_does_not_count_when_SMS_is_off()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", null, "+989121234567", "", "fa") { CurrentPassword = null });
        _identity.Logins.Add(("u0", "google", "g-1"));

        var result = await Create(smsEnabled: false).UnlinkAsync("u0", ExternalProviders.Google);

        Assert.Equal(AccountErrors.ExternalLastMethod, result.Error!.Code);
    }

    [Fact]
    public async Task Unlinking_a_provider_that_is_not_linked_is_not_found()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "a@x.io", null, Password, "fa"));

        Assert.Equal(ErrorKind.NotFound, (await Create().UnlinkAsync("u0", ExternalProviders.Facebook)).Error!.Kind);
    }
}
