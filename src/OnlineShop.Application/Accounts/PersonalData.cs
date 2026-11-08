using FluentValidation;
using OnlineShop.Application.Customers;

namespace OnlineShop.Application.Accounts;

public sealed record UpdateProfileRequest(string? DisplayName, string? PreferredLocale);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Password accounts confirm with their password; password-less accounts need a recent sign-in instead.</summary>
public sealed record DeleteAccountRequest(string? Password);

/// <summary>Claim set at every sign-in (unix seconds) and kept when the session is refreshed: "how long ago did the user prove who they are".</summary>
public static class AccountClaims
{
    public const string AuthTime = "auth_time";
}

/// <summary>Everything the shop stores about a customer, as one downloadable document (GDPR art. 15 and 20).</summary>
public sealed record PersonalDataExport(
    DateTime ExportedUtc,
    AccountData Account,
    IReadOnlyList<AddressView> Addresses,
    IReadOnlyList<OrderData> Orders,
    IReadOnlyList<CartItemData> Cart);

public sealed record AccountData(
    string Id,
    string? DisplayName,
    string? Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    string? PreferredLocale,
    DateTime CreatedUtc,
    bool TwoFactorEnabled,
    IReadOnlyList<string> ExternalLogins,
    IReadOnlyList<string> Roles);

public sealed record OrderData(Guid Id, DateTime OrderedUtc, string Status, decimal Subtotal, decimal Tax, decimal Shipping, decimal Total,
    IReadOnlyList<OrderItemData> Items);

public sealed record OrderItemData(string ProductName, decimal UnitPrice, int Quantity);

public sealed record CartItemData(string ProductName, int Quantity);

/// <summary>Identity operations for the profile, export and erasure use cases (kept apart from sign-in: <see cref="IAccountIdentity"/>).</summary>
public interface IAccountDataIdentity
{
    Task UpdateProfileAsync(string userId, string? displayName, string? preferredLocale);

    /// <summary>Changes the password and refreshes this session; other sessions end. Error codes as Identity reports them.</summary>
    Task<IReadOnlyList<string>> ChangePasswordAsync(string userId, string currentPassword, string newPassword);

    /// <summary>Checks the password without signing in; failures count towards the lockout.</summary>
    Task<bool> CheckPasswordAsync(string userId, string password);

    Task<AccountData?> GetAccountDataAsync(string userId);

    /// <summary>
    /// Removes every personal detail from the account row (email, phone, name, password, logins, tokens, roles),
    /// keeps the anonymous row so orders still balance, ends all sessions and signs out.
    /// </summary>
    Task EraseAsync(string userId);
}

public static class ProfileErrors
{
    public const string PasswordIncorrect = "account.password_incorrect";
    public const string NoPassword = "account.no_password";
    public const string LocaleNotSupported = "account.locale_not_supported";
    public const string StaffCannotDelete = "account.staff_cannot_delete";
    public const string ReauthenticationRequired = "account.reauthentication_required";
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(r => r.DisplayName!).MaximumLength(100).WithErrorCode(AccountErrors.DisplayNameTooLong).When(r => r.DisplayName is not null);
        RuleFor(r => r.PreferredLocale!).MaximumLength(8).WithErrorCode(ProfileErrors.LocaleNotSupported).When(r => r.PreferredLocale is not null);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty().WithErrorCode(AccountErrors.PasswordRequired).MaximumLength(PasswordRules.MaxLength);
        RuleFor(r => r.NewPassword).ValidPassword();
    }
}

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(r => r.Password!).MaximumLength(PasswordRules.MaxLength).When(r => r.Password is not null);
    }
}
