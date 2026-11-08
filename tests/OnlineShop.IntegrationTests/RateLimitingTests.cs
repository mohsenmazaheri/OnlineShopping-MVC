using System.Net;
using OnlineShop.IntegrationTests.TestSupport;

namespace OnlineShop.IntegrationTests;

/// <summary>Uses its own factory (own database) so the tight limit cannot affect other tests.</summary>
public sealed class RateLimitingTests(RateLimitingTests.TightLimitFactory factory)
    : IClassFixture<RateLimitingTests.TightLimitFactory>
{
    public sealed class TightLimitFactory : ApiFactory
    {
        protected override IReadOnlyDictionary<string, string?> ConfigurationOverrides => new Dictionary<string, string?>
        {
            ["RateLimiting:PermitPerWindow"] = "3",
            ["RateLimiting:WindowSeconds"] = "60"
        };
    }

    [Fact]
    public async Task Requests_over_the_limit_get_429_but_health_checks_are_never_limited()
    {
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            using var response = await client.GetAsync("/api/v1/store");
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);

        using var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}
