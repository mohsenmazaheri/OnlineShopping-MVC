using System.Security.Claims;
using System.Text.Json;
using OnlineShop.Api.Common;
using OnlineShop.Api.Security;
using OnlineShop.Application.Accounts;
using OnlineShop.Infrastructure.Identity;

namespace OnlineShop.Api.Endpoints;

/// <summary>The signed-in customer's profile, password, personal data download and account deletion.</summary>
public static class ProfileEndpoints
{
    // Readable by people too: the download is meant to be opened and understood.
    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/account").WithTags("Account").RequireAuthorization();

        group.MapPut("/profile", async (UpdateProfileRequest request, ClaimsPrincipal user, ProfileService profiles) =>
                (await profiles.UpdateAsync(user.GetUserId(), request)).ToHttpResult())
            .Validate<UpdateProfileRequest>()
            .WithName("UpdateProfile")
            .WithSummary("Change the display name and the language of emails and SMS.")
            .Produces<CurrentUser>();

        group.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal user, ProfileService profiles) =>
                (await profiles.ChangePasswordAsync(user.GetUserId(), request)).ToHttpResult())
            .Validate<ChangePasswordRequest>()
            .RequireRateLimiting(AccountEndpoints.AuthRateLimitPolicy)
            .WithName("ChangePassword")
            .WithSummary("Change the password (needs the current one); other sessions end.");

        group.MapGet("/personal-data", async (ClaimsPrincipal user, PersonalDataService personalData, HttpContext http, CancellationToken ct) =>
            {
                var export = await personalData.ExportAsync(user.GetUserId(), ct);
                if (!export.IsSuccess)
                    return ResultExtensions.ToProblem(export.Error!);

                http.Response.Headers.CacheControl = "no-store";
                return Results.File(JsonSerializer.SerializeToUtf8Bytes(export.Value, ExportJson), "application/json", "personal-data.json");
            })
            .RequireRateLimiting(AccountEndpoints.AuthRateLimitPolicy)
            .WithName("DownloadPersonalData")
            .WithSummary("Everything the shop stores about the signed-in customer, as a JSON file.")
            .Produces<PersonalDataExport>(contentType: "application/json");

        group.MapPost("/delete", async (DeleteAccountRequest request, ClaimsPrincipal user, PersonalDataService personalData, CancellationToken ct) =>
                (await personalData.DeleteAsync(user.GetUserId(), request, SignInClaims.AuthTime(user), ct)).ToHttpResult())
            .Validate<DeleteAccountRequest>()
            .RequireRateLimiting(AccountEndpoints.AuthRateLimitPolicy)
            .WithName("DeleteAccount")
            .WithSummary("Delete the account: personal data is erased, orders stay anonymously. Needs the password, or a recent sign-in for accounts without one.");

        return app;
    }
}
