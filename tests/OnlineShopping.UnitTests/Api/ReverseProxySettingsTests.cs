using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using OnlineShopping.Api.Security;

namespace OnlineShopping.UnitTests.Api;

public sealed class ReverseProxySettingsTests
{
    [Fact]
    public void By_default_only_loopback_may_forward_headers()
    {
        var options = Apply(new Dictionary<string, string?>());

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.True(options.KnownIPNetworks.Count + options.KnownProxies.Count > 0);
    }

    [Fact]
    public void Listed_proxies_are_trusted_and_blank_entries_ignored()
    {
        var options = Apply(new Dictionary<string, string?>
        {
            ["ReverseProxy:KnownProxies:0"] = "10.0.0.5",
            ["ReverseProxy:KnownProxies:1"] = ""
        });

        Assert.Contains(IPAddress.Parse("10.0.0.5"), options.KnownProxies);
    }

    [Fact]
    public void An_invalid_proxy_address_fails_at_startup()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Apply(new Dictionary<string, string?> { ["ReverseProxy:KnownProxies:0"] = "not-an-ip" }));

        Assert.Contains("not-an-ip", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_private_network_deployment_can_trust_every_host()
    {
        var options = Apply(new Dictionary<string, string?> { ["ReverseProxy:TrustForwardedHeadersFromAnyHost"] = "true" });

        Assert.Empty(options.KnownIPNetworks);
        Assert.Empty(options.KnownProxies);
    }

    private static ForwardedHeadersOptions Apply(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = new ForwardedHeadersOptions();
        ReverseProxySettings.Apply(options, configuration.GetSection(ReverseProxySettings.SectionName));
        return options;
    }
}
