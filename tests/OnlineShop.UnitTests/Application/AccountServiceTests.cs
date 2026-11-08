using Microsoft.Extensions.Options;
using OnlineShop.Application.Accounts;
using OnlineShop.Application.Common;
using OnlineShop.Infrastructure.Identity;
using OnlineShop.UnitTests.TestSupport;

namespace OnlineShop.UnitTests.Application;

public sealed class AccountServiceTests
{
    private const string Password = "correct horse 42";

    private readonly FakeAccountIdentity _identity = new();
    private readonly CapturingEmailSender _email = new();

    private AccountService Create(bool smsEnabled = false, AuthOptions? auth = null)
    {
        var store = new StoreOptions
        {
            Name = "Test Shop",
            DefaultLocale = "fa",
            Locales = "fa,en",
            PublicUrl = "https://shop.example.com/",
            PhoneRegion = "IR"
        };
        var normalizer = new PhoneNumberNormalizer();
        return new AccountService(_identity, new LoginIdentifierParser(normalizer), normalizer, _email,
            new FixedSmsCapability(smsEnabled), Options.Create(store), Options.Create(auth ?? new AuthOptions()));
    }

    [Fact]
    public async Task Registering_with_email_creates_the_account_signs_in_and_sends_a_confirmation_link()
    {
        var result = await Create().RegisterAsync(new RegisterRequest(" ali@example.com ", null, Password, "Ali", "en"));

        Assert.True(result.IsSuccess);
        Assert.Equal("ali@example.com", result.Value.Email);
        Assert.Equal(result.Value.Id, _identity.SignedInUserId);

        var mail = Assert.Single(_email.Sent);
        Assert.Equal("ali@example.com", mail.To);
        Assert.Contains("https://shop.example.com/en/account/confirm-email?userId=u1&token=confirm-u1", mail.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_or_missing_locales_use_the_store_default()
    {
        await Create().RegisterAsync(new RegisterRequest("ali@example.com", null, Password, null, "de"));

        Assert.Contains("/fa/account/confirm-email", _email.Sent.Single().TextBody, StringComparison.Ordinal);
        Assert.Equal("fa", _identity.Users.Single().Locale);
    }

    [Fact]
    public async Task A_taken_email_is_a_conflict()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "ali@example.com", null, Password, "fa"));

        var result = await Create().RegisterAsync(new RegisterRequest("ALI@example.com", null, Password, null, null));

        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(AccountErrors.EmailTaken, result.Error.Code);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Email_sign_up_can_be_turned_off()
    {
        var result = await Create(auth: new AuthOptions { EmailSignUp = false })
            .RegisterAsync(new RegisterRequest("ali@example.com", null, Password, null, null));

        Assert.Equal(AccountErrors.EmailSignUpDisabled, result.Error!.Code);
    }

    [Fact]
    public async Task Phone_sign_up_is_unavailable_without_an_SMS_provider()
    {
        var result = await Create(smsEnabled: false).RegisterAsync(new RegisterRequest(null, "09121234567", Password, null, null));

        Assert.Equal(AccountErrors.PhoneSignUpDisabled, result.Error!.Code);
        Assert.Empty(_identity.Users);
    }

    [Fact]
    public async Task With_SMS_the_phone_is_stored_in_E164_and_no_email_is_sent()
    {
        var result = await Create(smsEnabled: true).RegisterAsync(new RegisterRequest(null, "0912 123 4567", Password, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal("+989121234567", _identity.Users.Single().Phone);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task An_invalid_or_taken_phone_is_rejected()
    {
        var service = Create(smsEnabled: true);
        Assert.Equal(AccountErrors.PhoneInvalid,
            (await service.RegisterAsync(new RegisterRequest(null, "12345", Password, null, null))).Error!.Code);

        _identity.Users.Add(new FakeAccountIdentity.User("u0", null, "+989121234567", Password, "fa"));
        Assert.Equal(AccountErrors.PhoneTaken,
            (await service.RegisterAsync(new RegisterRequest(null, "09121234567", Password, null, null))).Error!.Code);
    }

    [Fact]
    public async Task Identity_password_rejections_become_a_validation_error()
    {
        _identity.NextCreateErrors = ["PasswordRequiresDigit"];

        var result = await Create().RegisterAsync(new RegisterRequest("ali@example.com", null, Password, null, null));

        Assert.Equal(AccountErrors.PasswordRejected, result.Error!.Code);
    }

    [Fact]
    public async Task Login_works_with_email_or_phone()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u1", "ali@example.com", "+989121234567", Password, "fa"));
        var service = Create();

        Assert.True((await service.LoginAsync(new LoginRequest("ali@example.com", Password, false))).IsSuccess);
        Assert.True((await service.LoginAsync(new LoginRequest("۰۹۱۲۱۲۳۴۵۶۷", Password, false))).IsSuccess);
    }

    [Theory]
    [InlineData("ali@example.com", "wrong password 1")]
    [InlineData("nobody@example.com", Password)]
    [InlineData("not-an-identifier", Password)]
    public async Task Every_kind_of_wrong_login_gets_the_same_answer(string identifier, string password)
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u1", "ali@example.com", null, Password, "fa"));

        var result = await Create().LoginAsync(new LoginRequest(identifier, password, false));

        Assert.Equal(AccountErrors.InvalidCredentials, result.Error!.Code);
        Assert.Null(_identity.SignedInUserId);
    }

    [Theory]
    [InlineData(SignInStatus.LockedOut, AccountErrors.LockedOut)]
    [InlineData(SignInStatus.TwoFactorRequired, AccountErrors.TwoFactorRequired)]
    [InlineData(SignInStatus.NotAllowed, AccountErrors.NotAllowed)]
    public async Task Sign_in_outcomes_map_to_error_codes(SignInStatus status, string code)
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u1", "ali@example.com", null, Password, "fa"));
        _identity.NextSignInStatus = status;

        var result = await Create().LoginAsync(new LoginRequest("ali@example.com", Password, false));

        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal(code, result.Error.Code);
    }

    [Fact]
    public async Task Forgot_password_reveals_nothing_about_unknown_emails()
    {
        await Create().ForgotPasswordAsync(new ForgotPasswordRequest("nobody@example.com", "en"));

        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Forgot_password_emails_a_reset_link_in_the_users_language()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u1", "ali@example.com", null, Password, "en"));

        await Create().ForgotPasswordAsync(new ForgotPasswordRequest("ali@example.com", "fa"));

        Assert.Contains("https://shop.example.com/en/account/reset-password?userId=u1&token=reset-u1",
            _email.Sent.Single().TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reset_password_with_a_bad_token_fails_and_with_a_good_token_succeeds()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u1", "ali@example.com", null, Password, "fa"));
        var service = Create();

        var bad = await service.ResetPasswordAsync(new ResetPasswordRequest("u1", "forged", "brand new pass 7"));
        Assert.Equal(AccountErrors.InvalidToken, bad.Error!.Code);

        var good = await service.ResetPasswordAsync(new ResetPasswordRequest("u1", "reset-u1", "brand new pass 7"));
        Assert.True(good.IsSuccess);
        Assert.Equal("brand new pass 7", _identity.Users.Single().CurrentPassword);
    }

    [Fact]
    public async Task Confirming_email_needs_the_right_token()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u1", "ali@example.com", null, Password, "fa"));
        var service = Create();

        Assert.Equal(AccountErrors.InvalidToken, (await service.ConfirmEmailAsync(new ConfirmEmailRequest("u1", "nope"))).Error!.Code);
        Assert.True((await service.ConfirmEmailAsync(new ConfirmEmailRequest("u1", "confirm-u1"))).IsSuccess);
        Assert.True(_identity.Users.Single().EmailConfirmed);
    }

    [Fact]
    public async Task Resending_confirmation_is_skipped_when_already_confirmed_and_refused_without_email()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u1", "ali@example.com", null, Password, "fa") { EmailConfirmed = true });
        _identity.Users.Add(new FakeAccountIdentity.User("u2", null, "+989121234567", Password, "fa"));
        var service = Create();

        Assert.True((await service.ResendConfirmationAsync("u1")).IsSuccess);
        Assert.Empty(_email.Sent);
        Assert.Equal(AccountErrors.NoEmail, (await service.ResendConfirmationAsync("u2")).Error!.Code);
    }
}
