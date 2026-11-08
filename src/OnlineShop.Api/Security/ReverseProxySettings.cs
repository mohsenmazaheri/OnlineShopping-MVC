using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace OnlineShop.Api.Security;

/// <summary>
/// Which hosts may set X-Forwarded-For/Proto (section <c>ReverseProxy</c>). The real client IP matters for
/// rate limiting and audit logs; trusting the header from anyone would let clients spoof it.
/// </summary>
public static class ReverseProxySettings
{
    public const string SectionName = "ReverseProxy";

    public static void Apply(ForwardedHeadersOptions options, IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(section);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Only for deployments where the API is unreachable from the internet (private Docker network).
        if (section.GetValue<bool>("TrustForwardedHeadersFromAnyHost"))
        {
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            return;
        }

        foreach (var proxy in section.GetSection("KnownProxies").Get<string[]>() ?? [])
        {
            if (string.IsNullOrWhiteSpace(proxy))
                continue;
            if (!IPAddress.TryParse(proxy, out var address))
                throw new InvalidOperationException($"{SectionName}:KnownProxies contains an invalid IP address: '{proxy}'.");
            options.KnownProxies.Add(address);
        }
    }
}
