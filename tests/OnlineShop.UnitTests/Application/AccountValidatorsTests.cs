using OnlineShop.Application.Accounts;

namespace OnlineShop.UnitTests.Application;

public sealed class AccountValidatorsTests
{
    private const string GoodPassword = "correct horse 42";

    private static IEnumerable<string> Codes(FluentValidation.Results.ValidationResult result) =>
        result.Errors.Select(e => e.ErrorCode);

    [Fact]
    public void Registration_needs_an_email_or_a_phone()
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest(null, " ", GoodPassword, null, null));

        Assert.Contains(AccountErrors.EmailOrPhoneRequired, Codes(result));
    }

    [Theory]
    [InlineData("ali@example.com", null)]
    [InlineData(null, "09121234567")]
    [InlineData("ali@example.com", "09121234567")]
    public void Registration_with_email_or_phone_and_a_good_password_is_valid(string? email, string? phone)
    {
        Assert.True(new RegisterRequestValidator().Validate(new RegisterRequest(email, phone, GoodPassword, "Ali", "fa")).IsValid);
    }

    [Theory]
    [InlineData("", AccountErrors.PasswordRequired)]
    [InlineData("short1", AccountErrors.PasswordTooShort)]
    [InlineData("twelve letters only", AccountErrors.PasswordNeedsDigit)]
    public void Weak_passwords_are_rejected_with_a_code(string password, string code)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest("ali@example.com", null, password, null, null));

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
        var result = new RegisterRequestValidator().Validate(new RegisterRequest("ali@", null, GoodPassword, null, null));

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
