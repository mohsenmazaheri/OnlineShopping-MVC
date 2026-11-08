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
    public const string PhoneRequired = "account.phone_required";
    public const string CodeRequired = "account.code_required";
    public const string CodeInvalid = "account.code_invalid";
    public const string CodeExpired = "account.code_expired";
    public const string CodeAttemptsExceeded = "account.code_attempts_exceeded";
    public const string CodeCooldown = "account.code_cooldown";
    public const string CodeLimitReached = "account.code_limit_reached";
    public const string SmsDisabled = "account.sms_disabled";
    public const string SmsFailed = "account.sms_failed";
    public const string ExternalProviderDisabled = "account.external_provider_disabled";
    public const string ExternalFailed = "account.external_failed";
    public const string ExternalNoEmail = "account.external_no_email";
    public const string ExternalEmailInUse = "account.external_email_in_use";
    public const string ExternalLoginInUse = "account.external_login_in_use";
    public const string ExternalNotLinked = "account.external_not_linked";
    public const string ExternalLastMethod = "account.external_last_method";
    public const string TwoFactorAlreadyEnabled = "account.two_factor_already_enabled";
    public const string TwoFactorNotEnabled = "account.two_factor_not_enabled";
    public const string TwoFactorRequiredForStaff = "account.two_factor_required_for_staff";
    public const string TwoFactorSessionExpired = "account.two_factor_session_expired";
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
        // Password sign-up is by email; phone accounts come from signing in with an SMS code.
        RuleFor(r => r.Email!)
            .NotEmpty().WithErrorCode(AccountErrors.EmailOrPhoneRequired)
            .Must(EmailRules.IsValid).WithErrorCode(AccountErrors.EmailInvalid);

        RuleFor(r => r.Password).ValidPassword();

        RuleFor(r => r.DisplayName!)
            .MaximumLength(100).WithErrorCode(AccountErrors.DisplayNameTooLong)
            .When(r => r.DisplayName is not null);
    }
}

public sealed class SendPhoneCodeRequestValidator : AbstractValidator<SendPhoneCodeRequest>
{
    public SendPhoneCodeRequestValidator()
    {
        RuleFor(r => r.Phone).NotEmpty().WithErrorCode(AccountErrors.PhoneRequired).MaximumLength(32).WithErrorCode(AccountErrors.PhoneInvalid);
    }
}

public sealed class PhoneSignInRequestValidator : AbstractValidator<PhoneSignInRequest>
{
    public PhoneSignInRequestValidator()
    {
        RuleFor(r => r.Phone).NotEmpty().WithErrorCode(AccountErrors.PhoneRequired).MaximumLength(32).WithErrorCode(AccountErrors.PhoneInvalid);
        RuleFor(r => r.Code).NotEmpty().WithErrorCode(AccountErrors.CodeRequired).MaximumLength(12).WithErrorCode(AccountErrors.CodeInvalid);
        RuleFor(r => r.DisplayName!)
            .MaximumLength(100).WithErrorCode(AccountErrors.DisplayNameTooLong)
            .When(r => r.DisplayName is not null);
    }
}

public sealed class ConfirmPhoneRequestValidator : AbstractValidator<ConfirmPhoneRequest>
{
    public ConfirmPhoneRequestValidator()
    {
        RuleFor(r => r.Phone).NotEmpty().WithErrorCode(AccountErrors.PhoneRequired).MaximumLength(32).WithErrorCode(AccountErrors.PhoneInvalid);
        RuleFor(r => r.Code).NotEmpty().WithErrorCode(AccountErrors.CodeRequired).MaximumLength(12).WithErrorCode(AccountErrors.CodeInvalid);
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
