using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OnlineShopping.Application.Accounts;
using OnlineShopping.Infrastructure.Identity;
using OnlineShopping.IntegrationTests.TestSupport;
using OnlineShopping.UnitTests.TestSupport;

namespace OnlineShopping.UnitTests.Infrastructure;

/// <summary>Identity side of two-step sign-in on a real (SQLite) user store; the cookie round trips are in the integration tests.</summary>
public sealed class TwoFactorIdentityTests : IDisposable
{
    private readonly IdentityTestHost _host = new(s => s.AddScoped<TwoFactorIdentity>());

    public void Dispose() => _host.Dispose();

    private TwoFactorIdentity Sut => _host.Get<TwoFactorIdentity>();
    private UserManager<AppUser> Users => _host.Users;

    private Task<AppUser> CreateUserAsync() => _host.CreateUserAsync();

    private async Task<string> CurrentCodeAsync(AppUser user) =>
        Totp.Code(await Users.GetAuthenticatorKeyAsync(user) ?? throw new InvalidOperationException("no key"));

    [Fact]
    public async Task The_authenticator_key_is_created_once_and_then_kept()
    {
        var user = await CreateUserAsync();

        var first = await Sut.GetOrCreateAuthenticatorKeyAsync(user.Id);
        var second = await Sut.GetOrCreateAuthenticatorKeyAsync(user.Id);

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Only_the_current_code_of_this_key_verifies()
    {
        var user = await CreateUserAsync();
        await Sut.GetOrCreateAuthenticatorKeyAsync(user.Id);

        Assert.True(await Sut.VerifyAuthenticatorCodeAsync(user.Id, await CurrentCodeAsync(user)));
        Assert.False(await Sut.VerifyAuthenticatorCodeAsync(user.Id, "000000"));
    }

    [Fact]
    public async Task Enabling_turns_it_on_with_ten_recovery_codes()
    {
        var user = await CreateUserAsync();
        await Sut.GetOrCreateAuthenticatorKeyAsync(user.Id);

        var codes = await Sut.EnableAsync(user.Id);

        Assert.Equal(TwoFactorIdentity.RecoveryCodeCount, codes.Count);
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Equal((true, 10), await Sut.GetStatusAsync(user.Id));
    }

    [Fact]
    public async Task Renewing_recovery_codes_invalidates_the_old_ones()
    {
        var user = await CreateUserAsync();
        await Sut.GetOrCreateAuthenticatorKeyAsync(user.Id);
        var old = await Sut.EnableAsync(user.Id);

        var fresh = await Sut.RegenerateRecoveryCodesAsync(user.Id);

        Assert.Empty(old.Intersect(fresh));
        var reloaded = await Users.FindByIdAsync(user.Id);
        Assert.False((await Users.RedeemTwoFactorRecoveryCodeAsync(reloaded!, old[0])).Succeeded);
        Assert.True((await Users.RedeemTwoFactorRecoveryCodeAsync(reloaded!, fresh[0])).Succeeded);
    }

    [Fact]
    public async Task Disabling_drops_the_key_and_the_recovery_codes()
    {
        var user = await CreateUserAsync();
        var key = await Sut.GetOrCreateAuthenticatorKeyAsync(user.Id);
        await Sut.EnableAsync(user.Id);

        await Sut.DisableAsync(user.Id);

        Assert.Equal((false, 0), await Sut.GetStatusAsync(user.Id));
        Assert.NotEqual(key, await Sut.GetOrCreateAuthenticatorKeyAsync(user.Id));
    }

    [Fact]
    public async Task The_second_step_without_a_first_step_is_not_allowed()
    {
        Assert.Equal((SignInStatus.NotAllowed, null), await Sut.AuthenticatorSignInAsync("123456", false, false));
        Assert.Equal((SignInStatus.NotAllowed, null), await Sut.RecoveryCodeSignInAsync("abc"));
    }

    [Fact]
    public async Task An_unknown_user_is_a_programming_error()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Sut.GetStatusAsync("missing"));
        Assert.False(await Sut.IsDeviceRememberedAsync("missing"));
    }

    [Fact]
    public async Task Sign_in_without_password_asks_for_the_second_step_only_when_it_is_on()
    {
        var signIn = _host.SignIn;
        var plain = await CreateUserAsync();
        var protectedUser = await CreateUserAsync();
        // As after enrolment: Identity asks for the step only when a provider (here the authenticator key) is usable.
        await Sut.GetOrCreateAuthenticatorKeyAsync(protectedUser.Id);
        await Sut.EnableAsync(protectedUser.Id);

        Assert.True((await signIn.SignInWithSecondStepAsync(plain, isPersistent: false)).Succeeded);
        var result = await signIn.SignInWithSecondStepAsync(protectedUser, isPersistent: false);

        Assert.True(result.RequiresTwoFactor);
        // Only the short-lived "pending second step" cookie was issued, not a session for the protected user.
        Assert.Contains(_host.Http.Response.Headers.SetCookie, c => c!.StartsWith(IdentityConstants.TwoFactorUserIdScheme, StringComparison.Ordinal));
    }
}
