using FluentValidation;

namespace OnlineShopping.Application.Accounts;

/// <summary>Section <c>Security</c>.</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// When on, staff can use back-office permissions only after a two-step sign-in (decided: off by default;
    /// the system admin can turn it on). Staff without an authenticator can still sign in and set one up.
    /// </summary>
    public bool RequireStaffTwoFactor { get; set; }
}

/// <summary>Two-step sign-in status of the signed-in account.</summary>
public sealed record TwoFactorStatus(bool Enabled, int RecoveryCodesLeft, bool RememberedDevice, bool RequiredForAccount);

/// <summary>What the authenticator app needs: a QR code (from <see cref="AuthenticatorUri"/>) or the key typed by hand.</summary>
public sealed record AuthenticatorSetup(string SharedKey, string AuthenticatorUri);

/// <summary>Recovery codes, shown once; each signs in once when the phone with the authenticator is lost.</summary>
public sealed record RecoveryCodes(IReadOnlyList<string> Codes);

public sealed record TwoFactorCodeRequest(string Code);

public sealed record TwoFactorSignInRequest(string Code, bool RememberMe, bool RememberDevice);

public sealed record RecoveryCodeSignInRequest(string RecoveryCode);

/// <summary>Identity's authenticator (TOTP), recovery codes and the second sign-in step.</summary>
public interface ITwoFactorIdentity
{
    Task<(bool Enabled, int RecoveryCodesLeft)> GetStatusAsync(string userId);

    /// <summary>The authenticator key, created on first use.</summary>
    Task<string> GetOrCreateAuthenticatorKeyAsync(string userId);

    Task<bool> VerifyAuthenticatorCodeAsync(string userId, string code);

    /// <summary>Turns two-step sign-in on and returns fresh recovery codes.</summary>
    Task<IReadOnlyList<string>> EnableAsync(string userId);

    /// <summary>Turns it off and rotates the key, so the old authenticator entry stops working.</summary>
    Task DisableAsync(string userId);

    Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(string userId);

    /// <summary>Second step after a first factor (password, SMS code, external provider) asked for it.</summary>
    Task<(SignInStatus Status, string? UserId)> AuthenticatorSignInAsync(string code, bool rememberMe, bool rememberDevice);

    Task<(SignInStatus Status, string? UserId)> RecoveryCodeSignInAsync(string recoveryCode);

    Task<bool> IsDeviceRememberedAsync(string userId);

    Task ForgetDeviceAsync();
}

public sealed class TwoFactorCodeRequestValidator : AbstractValidator<TwoFactorCodeRequest>
{
    public TwoFactorCodeRequestValidator() =>
        RuleFor(r => r.Code).NotEmpty().WithErrorCode(AccountErrors.CodeRequired).MaximumLength(12).WithErrorCode(AccountErrors.CodeInvalid);
}

public sealed class TwoFactorSignInRequestValidator : AbstractValidator<TwoFactorSignInRequest>
{
    public TwoFactorSignInRequestValidator() =>
        RuleFor(r => r.Code).NotEmpty().WithErrorCode(AccountErrors.CodeRequired).MaximumLength(12).WithErrorCode(AccountErrors.CodeInvalid);
}

public sealed class RecoveryCodeSignInRequestValidator : AbstractValidator<RecoveryCodeSignInRequest>
{
    public RecoveryCodeSignInRequestValidator() =>
        RuleFor(r => r.RecoveryCode).NotEmpty().WithErrorCode(AccountErrors.CodeRequired).MaximumLength(32).WithErrorCode(AccountErrors.CodeInvalid);
}
