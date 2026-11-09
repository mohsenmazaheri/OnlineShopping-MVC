using System.Net;
using System.Net.Http.Json;
using OnlineShopping.IntegrationTests.TestSupport;

namespace OnlineShopping.IntegrationTests;

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

/// <summary>Sign-in endpoints have their own, much tighter limit (password guessing, email flooding).</summary>
public sealed class AuthRateLimitingTests(AuthRateLimitingTests.TightAuthLimitFactory factory)
    : IClassFixture<AuthRateLimitingTests.TightAuthLimitFactory>
{
    public sealed class TightAuthLimitFactory : ApiFactory
    {
        protected override IReadOnlyDictionary<string, string?> ConfigurationOverrides => new Dictionary<string, string?>
        {
            ["RateLimiting:PermitPerWindow"] = "100",
            ["RateLimiting:AuthPermitPerWindow"] = "2",
            ["RateLimiting:WindowSeconds"] = "60"
        };
    }

    [Fact]
    public async Task Login_attempts_over_the_auth_limit_get_429_while_other_endpoints_still_answer()
    {
        using var client = factory.CreateClient();
        var login = new OnlineShopping.Application.Accounts.LoginRequest("someone@example.com", "wrong password 1", false);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/account/login", login);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests], statuses);
        using var store = await client.GetAsync("/api/v1/store");
        Assert.Equal(HttpStatusCode.OK, store.StatusCode);
    }
}
