using OnlineShopping.Application.Accounts;

namespace OnlineShopping.UnitTests.Application;

public sealed class AccountValidatorsTests
{
    private const string GoodPassword = "correct horse 42";

    private static IEnumerable<string> Codes(FluentValidation.Results.ValidationResult result) =>
        result.Errors.Select(e => e.ErrorCode);

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Password_registration_needs_an_email(string? email)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest(email, GoodPassword, null, null));

        Assert.Contains(AccountErrors.EmailOrPhoneRequired, Codes(result));
    }

    [Fact]
    public void Registration_with_email_and_a_good_password_is_valid()
    {
        Assert.True(new RegisterRequestValidator().Validate(new RegisterRequest("ali@example.com", GoodPassword, "Ali", "fa")).IsValid);
    }

    [Fact]
    public void Phone_code_requests_need_a_phone_and_a_code()
    {
        Assert.Contains(AccountErrors.PhoneRequired, Codes(new SendPhoneCodeRequestValidator().Validate(new SendPhoneCodeRequest("", null))));

        var signIn = Codes(new PhoneSignInRequestValidator().Validate(new PhoneSignInRequest("09121234567", "", null, null, false)));
        Assert.Contains(AccountErrors.CodeRequired, signIn);

        var confirm = Codes(new ConfirmPhoneRequestValidator().Validate(new ConfirmPhoneRequest("", "123456"))).ToList();
        Assert.Contains(AccountErrors.PhoneRequired, confirm);
        Assert.True(new ConfirmPhoneRequestValidator().Validate(new ConfirmPhoneRequest("09121234567", "123456")).IsValid);
    }

    [Theory]
    [InlineData("", AccountErrors.PasswordRequired)]
    [InlineData("short1", AccountErrors.PasswordTooShort)]
    [InlineData("twelve letters only", AccountErrors.PasswordNeedsDigit)]
    public void Weak_passwords_are_rejected_with_a_code(string password, string code)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest("ali@example.com", password, null, null));

        Assert.Contains(code, Codes(result));
    }

    [Fact]
    public void Absurdly_long_passwords_are_rejected()
    {
        var result = new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest("u", "t", new string('a', 200) + "1"));

        Assert.Contains(AccountErrors.PasswordTooLong, Codes(result));
    }

    [Fact]
    public void An_invalid_email_is_rejected()
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest("ali@", GoodPassword, null, null));

        Assert.Contains(AccountErrors.EmailInvalid, Codes(result));
    }

    [Fact]
    public void Login_needs_identifier_and_password()
    {
        var codes = Codes(new LoginRequestValidator().Validate(new LoginRequest("", "", false))).ToList();

        Assert.Contains(AccountErrors.IdentifierRequired, codes);
        Assert.Contains(AccountErrors.PasswordRequired, codes);
    }

    [Fact]
    public void Reset_and_confirmation_need_a_token()
    {
        Assert.Contains(AccountErrors.TokenRequired, Codes(new ConfirmEmailRequestValidator().Validate(new ConfirmEmailRequest("u", ""))));
        Assert.Contains(AccountErrors.TokenRequired, Codes(new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest("", "t", GoodPassword))));
    }

    [Fact]
    public void Forgot_password_needs_a_valid_email()
    {
        Assert.False(new ForgotPasswordRequestValidator().Validate(new ForgotPasswordRequest("nope", null)).IsValid);
        Assert.True(new ForgotPasswordRequestValidator().Validate(new ForgotPasswordRequest("a@b.co", "en")).IsValid);
    }
}
