using Microsoft.Extensions.Options;
using OnlineShop.Application.Common;

namespace OnlineShop.Api.Security;

/// <summary>
/// The API is only ever reached through the storefront (browser → web app proxy → API), so every absolute URL it builds
/// must use the storefront's public address, not the internal one (http://api:8080). This matters most for OAuth:
/// the redirect_uri sent to Google/Facebook/Microsoft must be https://{shop}/api/v1/account/external/.../oauth-callback.
/// </summary>
public sealed class PublicOriginMiddleware(RequestDelegate next, IOptions<StoreOptions> store)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var publicUrl = new Uri(store.Value.PublicUrl);
        context.Request.Scheme = publicUrl.Scheme;
        context.Request.Host = publicUrl.IsDefaultPort ? new HostString(publicUrl.Host) : new HostString(publicUrl.Host, publicUrl.Port);
        return next(context);
    }
}
