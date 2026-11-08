using FluentValidation;

namespace OnlineShop.Application.Accounts;

/// <summary>Stable error codes; the web app translates them for the user.</summary>
public static class AccountErrors
{
    public const string EmailOrPhoneRequired = "account.email_or_phone_required";
    public const string EmailInvalid = "account.email_invalid";
    public const string PhoneInvalid = "account.phone_invalid";
    public const string PasswordRequired = "account.password_required";
    public const string PasswordTooShort = "account.password_too_short";
    public const string PasswordTooLong = "account.password_too_long";
    public const string PasswordNeedsDigit = "account.password_needs_digit";
    public const string PasswordRejected = "account.password_rejected";
    public const string DisplayNameTooLong = "account.display_name_too_long";
    public const string IdentifierRequired = "account.identifier_required";
    public const string IdentifierInvalid = "account.identifier_invalid";
    public const string EmailTaken = "account.email_taken";
    public const string PhoneTaken = "account.phone_taken";
    public const string EmailSignUpDisabled = "account.email_sign_up_disabled";
    public const string PhoneSignUpDisabled = "account.phone_sign_up_disabled";
    public const string InvalidCredentials = "account.invalid_credentials";
    public const string LockedOut = "account.locked_out";
    public const string TwoFactorRequired = "account.two_factor_required";
    public const string NotAllowed = "account.not_allowed";
    public const string InvalidToken = "account.invalid_token";
    public const string NotFound = "account.not_found";
    public const string NoEmail = "account.no_email";
    public const string TokenRequired = "account.token_required";
}

/// <summary>Password rules shared by registration and reset (Identity enforces the same rules again).</summary>
public static class PasswordRules
{
    public const int MinLength = 12;

    // Long passphrases are welcome; the cap only prevents denial of service through huge hashing inputs.
    public const int MaxLength = 128;

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithErrorCode(AccountErrors.PasswordRequired)
            .MinimumLength(MinLength).WithErrorCode(AccountErrors.PasswordTooShort)
            .MaximumLength(MaxLength).WithErrorCode(AccountErrors.PasswordTooLong)
            .Must(p => p.Any(char.IsDigit)).WithErrorCode(AccountErrors.PasswordNeedsDigit);
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r)
            .Must(r => !string.IsNullOrWhiteSpace(r.Email) || !string.IsNullOrWhiteSpace(r.Phone))
            .WithErrorCode(AccountErrors.EmailOrPhoneRequired)
            .WithMessage("Enter an email address or a mobile number.")
            .OverridePropertyName("email");

        RuleFor(r => r.Email!)
            .Must(EmailRules.IsValid).WithErrorCode(AccountErrors.EmailInvalid)
            .When(r => !string.IsNullOrWhiteSpace(r.Email));

        RuleFor(r => r.Phone!)
            .MaximumLength(32).WithErrorCode(AccountErrors.PhoneInvalid)
            .When(r => !string.IsNullOrWhiteSpace(r.Phone));

        RuleFor(r => r.Password).ValidPassword();

        RuleFor(r => r.DisplayName!)
            .MaximumLength(100).WithErrorCode(AccountErrors.DisplayNameTooLong)
            .When(r => r.DisplayName is not null);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Identifier).NotEmpty().WithErrorCode(AccountErrors.IdentifierRequired).MaximumLength(EmailRules.MaxLength);
        RuleFor(r => r.Password).NotEmpty().WithErrorCode(AccountErrors.PasswordRequired).MaximumLength(PasswordRules.MaxLength);
    }
}

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(r => r.UserId).NotEmpty().WithErrorCode(AccountErrors.TokenRequired).MaximumLength(64);
        RuleFor(r => r.Token).NotEmpty().WithErrorCode(AccountErrors.TokenRequired).MaximumLength(2048);
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(r => r.Email).Must(EmailRules.IsValid).WithErrorCode(AccountErrors.EmailInvalid);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(r => r.UserId).NotEmpty().WithErrorCode(AccountErrors.TokenRequired).MaximumLength(64);
        RuleFor(r => r.Token).NotEmpty().WithErrorCode(AccountErrors.TokenRequired).MaximumLength(2048);
        RuleFor(r => r.NewPassword).ValidPassword();
    }
}
