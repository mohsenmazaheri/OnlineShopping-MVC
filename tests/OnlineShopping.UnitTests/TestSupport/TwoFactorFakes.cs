using Microsoft.Extensions.Options;
using OnlineShopping.Application.Accounts;

namespace OnlineShopping.UnitTests.TestSupport;

/// <summary>IOptionsMonitor with a fixed value.</summary>
internal sealed class StaticSecurity(SecurityOptions value) : IOptionsMonitor<SecurityOptions>
{
    public SecurityOptions CurrentValue => value;
    public SecurityOptions Get(string? name) => value;
    public IDisposable? OnChange(Action<SecurityOptions, string?> listener) => null;
}

/// <summary>In-memory two-factor state; the valid authenticator code is always "123456".</summary>
internal sealed class FakeTwoFactorIdentity : ITwoFactorIdentity
{
    public const string ValidCode = "123456";

    public HashSet<string> Enabled { get; } = [];
    public Dictionary<string, List<string>> Codes { get; } = [];
    public Dictionary<string, string> Keys { get; } = [];
    public string? PendingUserId { get; set; }
    public bool RememberedDevice { get; set; }

    public Task<(bool Enabled, int RecoveryCodesLeft)> GetStatusAsync(string userId) =>
        Task.FromResult((Enabled.Contains(userId), Codes.TryGetValue(userId, out var c) ? c.Count : 0));

    public Task<string> GetOrCreateAuthenticatorKeyAsync(string userId)
    {
        if (!Keys.TryGetValue(userId, out var key))
            Keys[userId] = key = "JBSWY3DPEHPK3PXP";
        return Task.FromResult(key);
    }

    public Task<bool> VerifyAuthenticatorCodeAsync(string userId, string code) => Task.FromResult(code == ValidCode);

    public Task<IReadOnlyList<string>> EnableAsync(string userId)
    {
        Enabled.Add(userId);
        return RegenerateRecoveryCodesAsync(userId);
    }

    public Task DisableAsync(string userId)
    {
        Enabled.Remove(userId);
        Keys.Remove(userId);
        Codes.Remove(userId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(string userId)
    {
        Codes[userId] = Enumerable.Range(1, 10).Select(i => $"code-{i}").ToList();
        return Task.FromResult<IReadOnlyList<string>>(Codes[userId]);
    }

    public Task<(SignInStatus Status, string? UserId)> AuthenticatorSignInAsync(string code, bool rememberMe, bool rememberDevice) =>
        Task.FromResult<(SignInStatus, string?)>(PendingUserId is null ? (SignInStatus.NotAllowed, null)
            : code == ValidCode ? (SignInStatus.Succeeded, PendingUserId) : (SignInStatus.InvalidCredentials, null));

    public Task<(SignInStatus Status, string? UserId)> RecoveryCodeSignInAsync(string recoveryCode)
    {
        if (PendingUserId is null)
            return Task.FromResult<(SignInStatus, string?)>((SignInStatus.NotAllowed, null));
        var codes = Codes.GetValueOrDefault(PendingUserId) ?? [];
        return Task.FromResult<(SignInStatus, string?)>(codes.Remove(recoveryCode) ? (SignInStatus.Succeeded, PendingUserId) : (SignInStatus.InvalidCredentials, null));
    }

    public Task<bool> IsDeviceRememberedAsync(string userId) => Task.FromResult(RememberedDevice);

    public Task ForgetDeviceAsync()
    {
        RememberedDevice = false;
        return Task.CompletedTask;
    }
}
