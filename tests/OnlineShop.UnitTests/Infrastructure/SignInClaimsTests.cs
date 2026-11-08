using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OnlineShop.Application.Accounts;
using OnlineShop.Infrastructure.Identity;
using OnlineShop.UnitTests.TestSupport;

namespace OnlineShop.UnitTests.Infrastructure;

public sealed class SignInClaimsTests : IDisposable
{
    private readonly IdentityTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public void Auth_time_round_trips_through_its_claim()
    {
        var at = new DateTimeOffset(2026, 3, 1, 8, 30, 0, TimeSpan.Zero);

        Assert.Equal(at, SignInClaims.AuthTime(Principal(SignInClaims.AuthTimeClaim(at))));
        Assert.Null(SignInClaims.AuthTime(Principal()));
        Assert.Null(SignInClaims.AuthTime(Principal(new Claim(AccountClaims.AuthTime, "not a number"))));
    }

    [Fact]
    public async Task A_refreshed_session_keeps_how_and_when_the_user_signed_in()
    {
        var current = Principal(
            new Claim(ClaimTypes.NameIdentifier, "u1"),
            SignInClaims.AuthTimeClaim(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)),
            new Claim(PermissionAuthorizationHandler.MfaClaimType, PermissionAuthorizationHandler.MfaClaimValue),
            new Claim(ClaimTypes.AuthenticationMethod, "Google"));
        var rebuilt = Principal(new Claim(ClaimTypes.NameIdentifier, "u1"), new Claim(ClaimTypes.Role, "Manager"));

        await SignInClaims.PreserveOnRefresh(new SecurityStampRefreshingPrincipalContext { CurrentPrincipal = current, NewPrincipal = rebuilt });

        Assert.True(rebuilt.HasClaim(PermissionAuthorizationHandler.MfaClaimType, PermissionAuthorizationHandler.MfaClaimValue));
        Assert.Equal("1700000000", rebuilt.FindFirstValue(AccountClaims.AuthTime));
        Assert.Equal("Google", rebuilt.FindFirstValue(ClaimTypes.AuthenticationMethod));
        Assert.True(rebuilt.IsInRole("Manager"));
        Assert.Single(rebuilt.FindAll(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public void Only_sign_in_claims_are_carried()
    {
        var carried = SignInClaims.Carried(Principal(new Claim(ClaimTypes.Role, "Admin"), new Claim(AccountClaims.AuthTime, "1")));

        Assert.Equal([AccountClaims.AuthTime], carried.Select(c => c.Type));
    }

    [Fact]
    public async Task Every_new_session_gets_the_sign_in_time()
    {
        var user = await _host.CreateUserAsync();
        var principal = await SignInAndCaptureAsync(() => _host.SignIn.SignInAsync(user, isPersistent: false));

        Assert.Equal(_host.Database.Clock.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            principal.FindFirstValue(AccountClaims.AuthTime));
    }

    /// <summary>Runs a sign-in and returns the principal the cookie handler was asked to store.</summary>
    private async Task<ClaimsPrincipal> SignInAndCaptureAsync(Func<Task> signIn)
    {
        var capture = new CapturingAuthenticationService();
        _host.Http.RequestServices = new OverridingServiceProvider(_host.Http.RequestServices, typeof(IAuthenticationService), capture);
        await signIn();
        return capture.SignedIn ?? throw new InvalidOperationException("Nothing was signed in.");
    }

    private sealed class CapturingAuthenticationService : IAuthenticationService
    {
        public ClaimsPrincipal? SignedIn { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(Microsoft.AspNetCore.Http.HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(Microsoft.AspNetCore.Http.HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(Microsoft.AspNetCore.Http.HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(Microsoft.AspNetCore.Http.HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            if (scheme == IdentityConstants.ApplicationScheme)
                SignedIn = principal;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(Microsoft.AspNetCore.Http.HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class OverridingServiceProvider(IServiceProvider inner, Type type, object instance) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == type ? instance : inner.GetService(serviceType);
    }
}
