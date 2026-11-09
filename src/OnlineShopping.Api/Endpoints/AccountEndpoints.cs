using System.Security.Claims;
using OnlineShopping.Api.Common;
using OnlineShopping.Api.Security;
using OnlineShopping.Application.Accounts;

namespace OnlineShopping.Api.Endpoints;

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

        MapPhoneEndpoints(group);

        group.MapPost("/confirm-email",async (ConfirmEmailRequest request, AccountService accounts) =>
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

    /// <summary>Sign-in with SMS codes (creates the account for a new number) and adding a verified phone.</summary>
    private static void MapPhoneEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/phone/send-code", async (SendPhoneCodeRequest request, AccountService accounts, CancellationToken ct) =>
                (await accounts.SendSignInCodeAsync(request, ct)).ToHttpResult())
            .Validate<SendPhoneCodeRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithName("SendPhoneSignInCode")
            .WithSummary("Text a one-time sign-in code to a mobile number (same answer whether or not it has an account).")
            .Produces<PhoneCodeSent>()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/phone/sign-in", async (PhoneSignInRequest request, AccountService accounts, CancellationToken ct) =>
                (await accounts.SignInWithCodeAsync(request, ct)).ToHttpResult())
            .Validate<PhoneSignInRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithName("PhoneSignIn")
            .WithSummary("Sign in with the SMS code; a new number gets a customer account.")
            .Produces<PhoneSignInResult>();

        group.MapPost("/phone/add/send-code", async (SendPhoneCodeRequest request, ClaimsPrincipal user, AccountService accounts, CancellationToken ct) =>
                (await accounts.SendAddPhoneCodeAsync(user.GetUserId(), request, ct)).ToHttpResult())
            .Validate<SendPhoneCodeRequest>()
            .RequireAuthorization()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithName("SendAddPhoneCode")
            .WithSummary("Text a code to a number the signed-in user wants to add.")
            .Produces<PhoneCodeSent>();

        group.MapPost("/phone/add/confirm", async (ConfirmPhoneRequest request, ClaimsPrincipal user, AccountService accounts, CancellationToken ct) =>
                (await accounts.ConfirmAddPhoneAsync(user.GetUserId(), request, ct)).ToHttpResult())
            .Validate<ConfirmPhoneRequest>()
            .RequireAuthorization()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithName("ConfirmAddPhone")
            .WithSummary("Save the number as the user's verified phone with the SMS code.")
            .Produces<CurrentUser>();
    }
}
