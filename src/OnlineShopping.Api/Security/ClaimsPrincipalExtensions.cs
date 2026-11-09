using System.Security.Claims;

namespace OnlineShopping.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in user's id from the signed auth cookie. Unlike ids in the URL or body, it cannot be changed by
    /// the client — use it for every ownership check (lesson from the IDOR fixes in BulkyWeb).
    /// Only call it on endpoints that require authentication.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? throw new InvalidOperationException("The current user has no NameIdentifier claim.");
    }
}
