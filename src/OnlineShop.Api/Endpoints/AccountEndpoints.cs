using System.Security.Claims;
using OnlineShop.Api.Common;
using OnlineShop.Api.Security;
using OnlineShop.Application.Accounts;

namespace OnlineShop.Api.Endpoints;

/// <summary>Customer sign-up, sign-in and password recovery. The session is an HttpOnly cookie.</summary>
public static class AccountEndpoints
{
    /// <summary>Rate-limit policy for endpoints attackers would hammer (password guessing, email flooding).</summary>
    public const string AuthRateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/account").WithTags("Account");

        group.MapPost("/register", async (RegisterRequest request, AccountService accounts, CancellationToken ct) =>
                (await accounts.RegisterAsync(request, ct)).ToHttpResult())
            .Validate<RegisterRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithName("Register")
            .WithSummary("Create a customer account with an email or mobile number and sign in.")
            .Produces<CurrentUser>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", async (LoginRequest request, AccountService accounts) =>
                (await accounts.LoginAsync(request)).ToHttpResult())
            .Validate<LoginRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithName("Login")
            .WithSummary("Sign in with email or mobile number and password.")
            .Produces<CurrentUser>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/logout", async (AccountService accounts) =>
            {
                await accounts.LogoutAsync();
                return TypedResults.NoContent();
            })
            .WithName("Logout")
            .WithSummary("Sign out (clears the session cookie).");

        group.MapGet("/me", async (ClaimsPrincipal user, AccountService accounts) =>
                (await accounts.GetCurrentAsync(user.GetUserId())).ToHttpResult())
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("The signed-in user, with roles and permissions.")
            .Produces<CurrentUser>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/confirm-email", async (ConfirmEmailRequest request, AccountService accounts) =>
                (await accounts.ConfirmEmailAsync(request)).ToHttpResult())
            .Validate<ConfirmEmailRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithName("ConfirmEmail")
            .WithSummary("Confirm an email address with the token from the confirmation email.");

        group.MapPost("/resend-confirmation", async (ClaimsPrincipal user, AccountService accounts, CancellationToken ct) =>
                (await accounts.ResendConfirmationAsync(user.GetUserId(), ct)).ToHttpResult())
            .RequireAuthorization()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithName("ResendEmailConfirmation")
            .WithSummary("Send the confirmation email again to the signed-in user.");

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, AccountService accounts, CancellationToken ct) =>
            {
                await accounts.ForgotPasswordAsync(request, ct);
                // Same answer whether or not the email has an account.
                return TypedResults.Accepted((string?)null);
            })
            .Validate<ForgotPasswordRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithName("ForgotPassword")
            .WithSummary("Email a password reset link, if an account uses this email.");

        group.MapPost("/reset-password", async (ResetPasswordRequest request, AccountService accounts) =>
                (await accounts.ResetPasswordAsync(request)).ToHttpResult())
            .Validate<ResetPasswordRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithName("ResetPassword")
            .WithSummary("Set a new password with the token from the reset email; signs out all other sessions.");

        return app;
    }
}
