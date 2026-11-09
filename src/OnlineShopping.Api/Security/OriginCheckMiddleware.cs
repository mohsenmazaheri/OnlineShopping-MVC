using Microsoft.Extensions.Options;
using OnlineShopping.Application.Common;

namespace OnlineShopping.Api.Security;

/// <summary>
/// CSRF defense for cookie-authenticated requests. Browsers always send <c>Origin</c> on cross-site
/// POST/PUT/PATCH/DELETE; such a request is rejected unless it comes from the storefront's own origin.
/// Requests without <c>Origin</c> come from non-browser clients (the web app's server, scripts), which carry no
/// ambient browser cookies, so they cannot be used for CSRF. Works together with SameSite=Lax cookies.
/// </summary>
public sealed class OriginCheckMiddleware(RequestDelegate next, IOptions<StoreOptions> store)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsUnsafe(context.Request.Method)
            && context.Request.Headers.Origin is { Count: > 0 } origin
            && !IsAllowed(origin.ToString(), store.Value.PublicOrigin))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(
                new { title = "Cross-site request rejected.", code = "security.origin_rejected" },
                context.RequestAborted);
        }

        return next(context);
    }

    internal static bool IsUnsafe(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method));

    internal static bool IsAllowed(string origin, string allowedOrigin) =>
        string.Equals(origin.TrimEnd('/'), allowedOrigin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
