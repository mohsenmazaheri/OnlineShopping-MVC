namespace OnlineShop.Api.Security;

/// <summary>
/// Adds security headers to every API response. The API only serves JSON, so the CSP forbids loading anything
/// and framing is denied. Browser-facing pages get their own (looser) headers from the Next.js app.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    internal static readonly IReadOnlyDictionary<string, string> Headers = new Dictionary<string, string>
    {
        ["X-Content-Type-Options"] = "nosniff",
        ["X-Frame-Options"] = "DENY",
        ["Referrer-Policy"] = "no-referrer",
        ["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'",
        ["Cross-Origin-Resource-Policy"] = "same-origin",
        ["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()"
    };

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            foreach (var (name, value) in Headers)
                context.Response.Headers[name] = value;

            // Never reveal the server technology.
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("X-Powered-By");
            return Task.CompletedTask;
        });

        return next(context);
    }
}
