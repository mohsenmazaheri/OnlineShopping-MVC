using System.Security.Claims;
using OnlineShopping.Api.Common;
using OnlineShopping.Api.Security;
using OnlineShopping.Application.Accounts;

namespace OnlineShopping.Api.Endpoints;

/// <summary>Two-step sign-in: setting up the authenticator, recovery codes, and the second sign-in step.</summary>
public static class TwoFactorEndpoints
{
    public static IEndpointRouteBuilder MapTwoFactorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/account/2fa").WithTags("Account");

        // Second step of a sign-in (after password, SMS code or provider answered "two_factor_required").
        group.MapPost("/verify", async (TwoFactorSignInRequest request, TwoFactorService service) =>
                (await service.SignInWithAuthenticatorAsync(request)).ToHttpResult())
            .Validate<TwoFactorSignInRequest>()
            .RequireRateLimiting(AccountEndpoints.AuthRateLimitPolicy)
            .WithName("TwoFactorSignIn")
            .WithSummary("Complete a sign-in with the authenticator app's code.")
            .Produces<CurrentUser>();

        group.MapPost("/recovery", async (RecoveryCodeSignInRequest request, TwoFactorService service) =>
                (await service.SignInWithRecoveryCodeAsync(request)).ToHttpResult())
            .Validate<RecoveryCodeSignInRequest>()
            .RequireRateLimiting(AccountEndpoints.AuthRateLimitPolicy)
            .WithName("RecoveryCodeSignIn")
            .WithSummary("Complete a sign-in with a one-time recovery code.")
            .Produces<CurrentUser>();

        var manage = group.MapGroup("").RequireAuthorization();

        manage.MapGet("/", async (ClaimsPrincipal user, TwoFactorService service) =>
                (await service.GetStatusAsync(user.GetUserId())).ToHttpResult())
            .WithName("GetTwoFactorStatus")
            .WithSummary("Whether two-step sign-in is on, recovery codes left, and whether it is required for this account.")
            .Produces<TwoFactorStatus>();

        manage.MapPost("/setup", async (ClaimsPrincipal user, TwoFactorService service) =>
                (await service.BeginSetupAsync(user.GetUserId())).ToHttpResult())
            .WithName("BeginTwoFactorSetup")
            .WithSummary("The key and otpauth:// URI (QR code) for the authenticator app.")
            .Produces<AuthenticatorSetup>();

        manage.MapPost("/enable", async (TwoFactorCodeRequest request, ClaimsPrincipal user, TwoFactorService service) =>
                (await service.EnableAsync(user.GetUserId(), request.Code)).ToHttpResult())
            .Validate<TwoFactorCodeRequest>()
            .RequireRateLimiting(AccountEndpoints.AuthRateLimitPolicy)
            .WithName("EnableTwoFactor")
            .WithSummary("Turn on two-step sign-in with a first code from the app; returns recovery codes (shown once).")
            .Produces<RecoveryCodes>();

        manage.MapPost("/disable", async (TwoFactorCodeRequest request, ClaimsPrincipal user, TwoFactorService service) =>
                (await service.DisableAsync(user.GetUserId(), request.Code)).ToHttpResult())
            .Validate<TwoFactorCodeRequest>()
            .RequireRateLimiting(AccountEndpoints.AuthRateLimitPolicy)
            .WithName("DisableTwoFactor")
            .WithSummary("Turn off two-step sign-in (needs a current code; refused for staff when it is required).");

        manage.MapPost("/recovery-codes", async (TwoFactorCodeRequest request, ClaimsPrincipal user, TwoFactorService service) =>
                (await service.RegenerateRecoveryCodesAsync(user.GetUserId(), request.Code)).ToHttpResult())
            .Validate<TwoFactorCodeRequest>()
            .RequireRateLimiting(AccountEndpoints.AuthRateLimitPolicy)
            .WithName("RegenerateRecoveryCodes")
            .WithSummary("Replace all recovery codes (needs a current code).")
            .Produces<RecoveryCodes>();

        manage.MapPost("/forget-device", async (TwoFactorService service) =>
            {
                await service.ForgetDeviceAsync();
                return TypedResults.NoContent();
            })
            .WithName("ForgetTwoFactorDevice")
            .WithSummary("Ask for the second step again on this device.");

        return app;
    }
}
