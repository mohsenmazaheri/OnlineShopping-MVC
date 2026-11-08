namespace OnlineShop.Application.Accounts;

public sealed record RegisterRequest(string? Email, string? Phone, string Password, string? DisplayName, string? Locale);

public sealed record LoginRequest(string Identifier, string Password, bool RememberMe);

public sealed record ConfirmEmailRequest(string UserId, string Token);

public sealed record ForgotPasswordRequest(string Email, string? Locale);

public sealed record ResetPasswordRequest(string UserId, string Token, string NewPassword);

/// <summary>The signed-in user as the storefront and admin panel see it.</summary>
public sealed record CurrentUser(
    string Id,
    string? DisplayName,
    string? Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    string? PreferredLocale,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions)
{
    public bool IsStaff => Permissions.Count > 0;
}

/// <summary>How a login identifier was interpreted.</summary>
public sealed record LoginIdentifier(string? Email, string? PhoneE164);

public enum SignInStatus
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
    TwoFactorRequired,
    NotAllowed
}

/// <summary>Identity operations the application needs; implemented with ASP.NET Core Identity in Infrastructure.</summary>
public interface IAccountIdentity
{
    Task<bool> EmailInUseAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> PhoneInUseAsync(string phoneE164, CancellationToken cancellationToken = default);

    /// <summary>Creates a customer account. Returns the new user id, or the password/validation errors.</summary>
    Task<IdentityCreateResult> CreateCustomerAsync(
        string? email, string? phoneE164, string password, string? displayName, string? locale,
        CancellationToken cancellationToken = default);

    /// <summary>Checks the password and, on success, issues the auth cookie. Wrong identifier and wrong password look the same.</summary>
    Task<(SignInStatus Status, string? UserId)> PasswordSignInAsync(LoginIdentifier identifier, string password, bool rememberMe);

    /// <summary>Signs in a user who just proved who they are (e.g. right after registering).</summary>
    Task SignInAsync(string userId, bool rememberMe);

    Task SignOutAsync();

    Task<CurrentUser?> FindAsync(string userId);

    /// <summary>Null when the user does not exist or has no email.</summary>
    Task<string?> CreateEmailConfirmationTokenAsync(string userId);

    Task<bool> ConfirmEmailAsync(string userId, string token);

    /// <summary>Null when no account has this email (callers must not reveal that).</summary>
    Task<(string UserId, string Token, string? Locale)?> CreatePasswordResetTokenAsync(string email);

    Task<IdentityCreateResult> ResetPasswordAsync(string userId, string token, string newPassword);
}

public sealed record IdentityCreateResult(string? UserId, IReadOnlyList<string> ErrorCodes)
{
    public bool Succeeded => ErrorCodes.Count == 0;

    public static IdentityCreateResult Success(string userId) => new(userId, []);

    public static IdentityCreateResult Failed(IReadOnlyList<string> errorCodes) => new(null, errorCodes);
}

/// <summary>Normalizes user-typed phone numbers to E.164 (+989121234567).</summary>
public interface IPhoneNumberNormalizer
{
    /// <returns>The E.164 form, or null when the input is not a valid phone number.</returns>
    string? Normalize(string input, string defaultRegion);
}
