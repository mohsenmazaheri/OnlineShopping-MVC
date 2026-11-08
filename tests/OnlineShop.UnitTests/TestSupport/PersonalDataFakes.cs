using OnlineShop.Application.Accounts;

namespace OnlineShop.UnitTests.TestSupport;

/// <summary>In-memory <see cref="IAccountDataIdentity"/> over the users of a <see cref="FakeAccountIdentity"/>.</summary>
internal sealed class FakeAccountDataIdentity(FakeAccountIdentity accounts) : IAccountDataIdentity
{
    public Dictionary<string, (string? Name, string? Locale)> Profiles { get; } = [];
    public List<string> Erased { get; } = [];
    public int PasswordChecks { get; private set; }

    public Task UpdateProfileAsync(string userId, string? displayName, string? preferredLocale)
    {
        Profiles[userId] = (displayName, preferredLocale);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = accounts.Users.Single(u => u.Id == userId);
        if (user.CurrentPassword != currentPassword)
            return Task.FromResult<IReadOnlyList<string>>(["PasswordMismatch"]);
        if (newPassword.Length < PasswordRules.MinLength)
            return Task.FromResult<IReadOnlyList<string>>(["PasswordTooShort"]);
        user.CurrentPassword = newPassword;
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    public Task<bool> CheckPasswordAsync(string userId, string password)
    {
        PasswordChecks++;
        return Task.FromResult(accounts.Users.Single(u => u.Id == userId).CurrentPassword == password);
    }

    public Task<AccountData?> GetAccountDataAsync(string userId)
    {
        var user = accounts.Users.SingleOrDefault(u => u.Id == userId && !Erased.Contains(u.Id));
        return Task.FromResult(user is null
            ? null
            : new AccountData(user.Id, null, user.Email, user.EmailConfirmed, user.Phone, user.Phone is not null, user.Locale,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), false, ["Google"], ["Customer"]));
    }

    public Task EraseAsync(string userId)
    {
        Erased.Add(userId);
        return Task.CompletedTask;
    }
}
