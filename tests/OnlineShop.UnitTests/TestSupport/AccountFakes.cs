using OnlineShop.Application.Accounts;
using OnlineShop.Application.Notifications;

namespace OnlineShop.UnitTests.TestSupport;

/// <summary>In-memory <see cref="IAccountIdentity"/> for testing AccountService rules without Identity.</summary>
internal sealed class FakeAccountIdentity : IAccountIdentity
{
    public sealed record User(string Id, string? Email, string? Phone, string Password, string? Locale)
    {
        public bool EmailConfirmed { get; set; }
        public string? CurrentPassword { get; set; } = Password;
    }

    public List<User> Users { get; } = [];
    public string? SignedInUserId { get; private set; }
    public SignInStatus NextSignInStatus { get; set; } = SignInStatus.Succeeded;
    public IReadOnlyList<string>? NextCreateErrors { get; set; }

    public Task<bool> EmailInUseAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> PhoneInUseAsync(string phoneE164, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Any(u => u.Phone == phoneE164));

    public Task<IdentityCreateResult> CreateCustomerAsync(string? email, string? phoneE164, string password,
        string? displayName, string? locale, CancellationToken cancellationToken = default)
    {
        if (NextCreateErrors is { } errors)
            return Task.FromResult(IdentityCreateResult.Failed(errors));
        var user = new User($"u{Users.Count + 1}", email, phoneE164, password, locale);
        Users.Add(user);
        return Task.FromResult(IdentityCreateResult.Success(user.Id));
    }

    public Task<(SignInStatus Status, string? UserId)> PasswordSignInAsync(LoginIdentifier identifier, string password, bool rememberMe)
    {
        var user = Users.SingleOrDefault(u => (identifier.Email is not null && u.Email == identifier.Email) || (identifier.PhoneE164 is not null && u.Phone == identifier.PhoneE164));
        if (user is null || user.CurrentPassword != password)
            return Task.FromResult<(SignInStatus, string?)>((SignInStatus.InvalidCredentials, null));
        if (NextSignInStatus != SignInStatus.Succeeded)
            return Task.FromResult<(SignInStatus, string?)>((NextSignInStatus, null));
        SignedInUserId = user.Id;
        return Task.FromResult<(SignInStatus, string?)>((SignInStatus.Succeeded, user.Id));
    }

    /// <summary>Accounts with two-step sign-in on: SignInAsync starts the second step instead of signing in.</summary>
    public HashSet<string> TwoFactorAccounts { get; } = [];

    /// <summary>Users that FindAsync reports as staff (Manager role, a back-office permission).</summary>
    public HashSet<string> StaffUserIds { get; } = [];

    public Task<SignInStatus> SignInAsync(string userId, bool rememberMe)
    {
        if (TwoFactorAccounts.Contains(userId))
            return Task.FromResult(SignInStatus.TwoFactorRequired);
        SignedInUserId = userId;
        return Task.FromResult(SignInStatus.Succeeded);
    }

    public Task SignOutAsync()
    {
        SignedInUserId = null;
        return Task.CompletedTask;
    }

    public Task<CurrentUser?> FindAsync(string userId)
    {
        var user = Users.SingleOrDefault(u => u.Id == userId);
        return Task.FromResult(user is null
            ? null
            : StaffUserIds.Contains(user.Id)
                ? new CurrentUser(user.Id, null, user.Email, user.EmailConfirmed, user.Phone, user.Phone is not null, user.Locale, ["Manager"], ["catalog.manage"])
                : new CurrentUser(user.Id, null, user.Email, user.EmailConfirmed, user.Phone, user.Phone is not null, user.Locale, ["Customer"], []));
    }

    public Task<string?> CreateEmailConfirmationTokenAsync(string userId) =>
        Task.FromResult(Users.SingleOrDefault(u => u.Id == userId)?.Email is null ? null : $"confirm-{userId}");

    public Task<bool> ConfirmEmailAsync(string userId, string token)
    {
        var user = Users.SingleOrDefault(u => u.Id == userId);
        if (user is null || token != $"confirm-{userId}")
            return Task.FromResult(false);
        user.EmailConfirmed = true;
        return Task.FromResult(true);
    }

    public Task<(string UserId, string Token, string? Locale)?> CreatePasswordResetTokenAsync(string email)
    {
        var user = Users.SingleOrDefault(u => u.Email == email);
        return Task.FromResult<(string, string, string?)?>(user is null ? null : (user.Id, $"reset-{user.Id}", user.Locale));
    }

    public bool LockedOut { get; set; }

    /// <summary>(UserId, Provider, ProviderKey) external logins.</summary>
    public List<(string UserId, string Provider, string Key)> Logins { get; } = [];

    public Task<string?> FindUserIdByEmailAsync(string email) =>
        Task.FromResult(Users.SingleOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase))?.Id);

    public Task<string?> FindUserIdByExternalLoginAsync(string provider, string providerKey) =>
        Task.FromResult<string?>(Logins.SingleOrDefault(l => l.Provider == provider && l.Key == providerKey).UserId);

    public Task<IdentityCreateResult> CreateExternalCustomerAsync(string email, bool emailConfirmed, string? displayName, string? locale)
    {
        var user = new User($"u{Users.Count + 1}", email, null, "", locale) { EmailConfirmed = emailConfirmed, CurrentPassword = null };
        Users.Add(user);
        return Task.FromResult(IdentityCreateResult.Success(user.Id));
    }

    public Task<IdentityCreateResult> AddExternalLoginAsync(string userId, string provider, string providerKey, string? displayName)
    {
        if (Logins.Any(l => l.Provider == provider && l.Key == providerKey))
            return Task.FromResult(IdentityCreateResult.Failed(["LoginAlreadyAssociated"]));
        Logins.Add((userId, provider, providerKey));
        return Task.FromResult(IdentityCreateResult.Success(userId));
    }

    public Task<IReadOnlyList<string>> GetExternalLoginsAsync(string userId) =>
        Task.FromResult<IReadOnlyList<string>>(Logins.Where(l => l.UserId == userId).Select(l => l.Provider).Distinct().ToList());

    public Task<IdentityCreateResult> RemoveExternalLoginAsync(string userId, string provider)
    {
        Logins.RemoveAll(l => l.UserId == userId && l.Provider == provider);
        return Task.FromResult(IdentityCreateResult.Success(userId));
    }

    public Task<bool> HasPasswordAsync(string userId) =>
        Task.FromResult(!string.IsNullOrEmpty(Users.SingleOrDefault(u => u.Id == userId)?.CurrentPassword));

    public Task<string?> FindUserIdByPhoneAsync(string phoneE164, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.SingleOrDefault(u => u.Phone == phoneE164)?.Id);

    public Task<IdentityCreateResult> CreatePhoneCustomerAsync(string phoneE164, string? displayName, string? locale,
        CancellationToken cancellationToken = default)
    {
        var user = new User($"u{Users.Count + 1}", null, phoneE164, "", locale) { CurrentPassword = null };
        Users.Add(user);
        return Task.FromResult(IdentityCreateResult.Success(user.Id));
    }

    public Task<bool> IsLockedOutAsync(string userId) => Task.FromResult(LockedOut);

    public Task<IdentityCreateResult> SetConfirmedPhoneAsync(string userId, string phoneE164)
    {
        if (Users.Any(u => u.Phone == phoneE164 && u.Id != userId))
            return Task.FromResult(IdentityCreateResult.Failed(["DuplicatePhoneNumber"]));
        var index = Users.FindIndex(u => u.Id == userId);
        Users[index] = Users[index] with { Phone = phoneE164 };
        return Task.FromResult(IdentityCreateResult.Success(userId));
    }

    public Task<IdentityCreateResult> ResetPasswordAsync(string userId, string token, string newPassword)
    {
        var user = Users.SingleOrDefault(u => u.Id == userId);
        if (user is null || token != $"reset-{userId}")
            return Task.FromResult(IdentityCreateResult.Failed(["InvalidToken"]));
        user.CurrentPassword = newPassword;
        return Task.FromResult(IdentityCreateResult.Success(userId));
    }
}

internal sealed class CapturingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class FixedSmsCapability(bool enabled) : ISmsCapability
{
    public bool IsEnabled => enabled;
}

/// <summary>Records codes instead of texting them; can be told to fail like a provider outage.</summary>
internal sealed class CapturingSmsSender : ISmsSender
{
    public List<(string Phone, string Code, string Locale)> Sent { get; } = [];
    public bool Fail { get; set; }

    public string LastCodeFor(string phone) => Sent.Last(s => s.Phone == phone).Code;

    public Task SendVerificationCodeAsync(string phoneE164, string code, string locale, CancellationToken cancellationToken = default)
    {
        if (Fail)
            throw new SmsDeliveryException("provider down");
        Sent.Add((phoneE164, code, locale));
        return Task.CompletedTask;
    }
}
