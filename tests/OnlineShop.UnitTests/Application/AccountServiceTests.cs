using Microsoft.Extensions.Options;
using OnlineShop.Application.Accounts;
using OnlineShop.Application.Common;
using OnlineShop.Infrastructure.Identity;
using OnlineShop.UnitTests.TestSupport;

namespace OnlineShop.UnitTests.Application;

public sealed class AccountServiceTests : IDisposable
{
    private const string Password = "correct horse 42";

    private readonly FakeAccountIdentity _identity = new();
    private readonly CapturingEmailSender _email = new();
    private readonly CapturingSmsSender _sms = new();
    private readonly SqliteTestDatabase _database = new();
    private readonly List<IDisposable> _contexts = [];

    public void Dispose()
    {
        _contexts.ForEach(c => c.Dispose());
        _database.Dispose();
    }

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
        var db = _database.CreateContext();
        _contexts.Add(db);
        var codes = new PhoneCodeService(db, _sms, new FixedSmsCapability(smsEnabled),
            Options.Create(new PhoneCodeOptions()), _database.Clock);
        var normalizer = new PhoneNumberNormalizer();
        return new AccountService(_identity, new LoginIdentifierParser(normalizer), normalizer, codes, _email,
            Options.Create(store), Options.Create(auth ?? new AuthOptions()));
    }

    [Fact]
    public async Task Registering_with_email_creates_the_account_signs_in_and_sends_a_confirmation_link()
    {
        var result = await Create().RegisterAsync(new RegisterRequest(" ali@example.com ", Password, "Ali", "en"));

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
        await Create().RegisterAsync(new RegisterRequest("ali@example.com", Password, null, "de"));

        Assert.Contains("/fa/account/confirm-email", _email.Sent.Single().TextBody, StringComparison.Ordinal);
        Assert.Equal("fa", _identity.Users.Single().Locale);
    }

    [Fact]
    public async Task A_taken_email_is_a_conflict()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "ali@example.com", null, Password, "fa"));

        var result = await Create().RegisterAsync(new RegisterRequest("ALI@example.com", Password, null, null));

        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(AccountErrors.EmailTaken, result.Error.Code);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Email_sign_up_can_be_turned_off()
    {
        var result = await Create(auth: new AuthOptions { EmailSignUp = false })
            .RegisterAsync(new RegisterRequest("ali@example.com", Password, null, null));

        Assert.Equal(AccountErrors.EmailSignUpDisabled, result.Error!.Code);
    }

    [Fact]
    public async Task Sign_in_codes_need_an_SMS_provider()
    {
        var result = await Create(smsEnabled: false).SendSignInCodeAsync(new SendPhoneCodeRequest("09121234567", "fa"));

        Assert.Equal(AccountErrors.SmsDisabled, result.Error!.Code);
        Assert.Empty(_sms.Sent);
    }

    [Fact]
    public async Task A_new_number_signing_in_with_a_code_gets_a_verified_customer_account()
    {
        var service = Create(smsEnabled: true);

        var sent = await service.SendSignInCodeAsync(new SendPhoneCodeRequest("0912 123 4567", "en"));
        Assert.True(sent.IsSuccess);
        var (phone, code, locale) = _sms.Sent.Single();
        Assert.Equal(("+989121234567", "en"), (phone, locale));

        var result = await service.SignInWithCodeAsync(new PhoneSignInRequest("09121234567", code, "Sara", "en", false));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsNewAccount);
        Assert.Equal("+989121234567", _identity.Users.Single().Phone);
        Assert.Equal(result.Value.User.Id, _identity.SignedInUserId);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task An_existing_number_signs_in_to_its_account()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "ali@example.com", "+989121234567", Password, "fa"));
        var service = Create(smsEnabled: true);
        await service.SendSignInCodeAsync(new SendPhoneCodeRequest("09121234567", null));

        var result = await service.SignInWithCodeAsync(new PhoneSignInRequest("09121234567", _sms.LastCodeFor("+989121234567"), null, null, true));

        Assert.False(result.Value.IsNewAccount);
        Assert.Equal("u0", _identity.SignedInUserId);
        Assert.Single(_identity.Users);
    }

    [Fact]
    public async Task A_wrong_code_signs_nobody_in()
    {
        var service = Create(smsEnabled: true);
        await service.SendSignInCodeAsync(new SendPhoneCodeRequest("09121234567", null));
        var wrong = _sms.LastCodeFor("+989121234567") == "000000" ? "111111" : "000000";

        var result = await service.SignInWithCodeAsync(new PhoneSignInRequest("09121234567", wrong, null, null, false));

        Assert.Equal(AccountErrors.CodeInvalid, result.Error!.Code);
        Assert.Null(_identity.SignedInUserId);
        Assert.Empty(_identity.Users);
    }

    [Fact]
    public async Task A_locked_account_cannot_bypass_the_lock_with_a_code()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", null, "+989121234567", Password, "fa"));
        _identity.LockedOut = true;
        var service = Create(smsEnabled: true);
        await service.SendSignInCodeAsync(new SendPhoneCodeRequest("09121234567", null));

        var result = await service.SignInWithCodeAsync(new PhoneSignInRequest("09121234567", _sms.LastCodeFor("+989121234567"), null, null, false));

        Assert.Equal(AccountErrors.LockedOut, result.Error!.Code);
        Assert.Null(_identity.SignedInUserId);
    }

    [Fact]
    public async Task New_numbers_are_refused_when_phone_sign_up_is_off_but_existing_ones_still_sign_in()
    {
        var service = Create(smsEnabled: true, auth: new AuthOptions { PhoneSignUp = false });
        await service.SendSignInCodeAsync(new SendPhoneCodeRequest("09121234567", null));

        var result = await service.SignInWithCodeAsync(new PhoneSignInRequest("09121234567", _sms.LastCodeFor("+989121234567"), null, null, false));

        Assert.Equal(AccountErrors.PhoneSignUpDisabled, result.Error!.Code);
        Assert.Empty(_identity.Users);
    }

    [Fact]
    public async Task Invalid_numbers_are_rejected_before_any_SMS()
    {
        var result = await Create(smsEnabled: true).SendSignInCodeAsync(new SendPhoneCodeRequest("12345", null));

        Assert.Equal(AccountErrors.PhoneInvalid, result.Error!.Code);
        Assert.Empty(_sms.Sent);
    }

    [Fact]
    public async Task A_signed_in_user_can_add_a_verified_phone()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "ali@example.com", null, Password, "fa"));
        var service = Create(smsEnabled: true);

        Assert.True((await service.SendAddPhoneCodeAsync("u0", new SendPhoneCodeRequest("09121234567", null))).IsSuccess);
        var result = await service.ConfirmAddPhoneAsync("u0", new ConfirmPhoneRequest("09121234567", _sms.LastCodeFor("+989121234567")));

        Assert.True(result.IsSuccess);
        Assert.Equal("+989121234567", _identity.Users.Single().Phone);
    }

    [Fact]
    public async Task Adding_a_number_another_account_uses_is_refused_before_texting_it()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "ali@example.com", null, Password, "fa"));
        _identity.Users.Add(new FakeAccountIdentity.User("u1", null, "+989121234567", Password, "fa"));

        var result = await Create(smsEnabled: true).SendAddPhoneCodeAsync("u0", new SendPhoneCodeRequest("09121234567", null));

        Assert.Equal(AccountErrors.PhoneTaken, result.Error!.Code);
        Assert.Empty(_sms.Sent);
    }

    [Fact]
    public async Task A_sign_in_code_cannot_be_used_to_add_a_phone_to_another_account()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", "ali@example.com", null, Password, "fa"));
        var service = Create(smsEnabled: true);
        await service.SendSignInCodeAsync(new SendPhoneCodeRequest("09121234567", null));

        var result = await service.ConfirmAddPhoneAsync("u0", new ConfirmPhoneRequest("09121234567", _sms.LastCodeFor("+989121234567")));

        Assert.Equal(AccountErrors.CodeInvalid, result.Error!.Code);
        Assert.Null(_identity.Users.Single().Phone);
    }

    [Fact]
    public async Task Identity_password_rejections_become_a_validation_error()
    {
        _identity.NextCreateErrors = ["PasswordRequiresDigit"];

        var result = await Create().RegisterAsync(new RegisterRequest("ali@example.com", Password, null, null));

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

    [Fact]
    public async Task An_SMS_code_alone_does_not_sign_in_an_account_with_two_step_sign_in()
    {
        _identity.Users.Add(new FakeAccountIdentity.User("u0", null, "+989121234567", Password, "fa"));
        _identity.TwoFactorAccounts.Add("u0");
        var service = Create(smsEnabled: true);
        await service.SendSignInCodeAsync(new SendPhoneCodeRequest("09121234567", null));

        var result = await service.SignInWithCodeAsync(new PhoneSignInRequest("09121234567", _sms.LastCodeFor("+989121234567"), null, null, false));

        Assert.Equal(AccountErrors.TwoFactorRequired, result.Error!.Code);
        Assert.Null(_identity.SignedInUserId);
    }
}
