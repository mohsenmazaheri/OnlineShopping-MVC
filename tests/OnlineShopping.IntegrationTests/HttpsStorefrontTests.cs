using System.Net.Http.Json;
using OnlineShopping.Application.Accounts;
using OnlineShopping.Infrastructure;
using OnlineShopping.IntegrationTests.TestSupport;

namespace OnlineShopping.IntegrationTests;

/// <summary>Behind an HTTPS storefront the API marks the session cookie Secure, even though it is reached over plain HTTP internally.</summary>
public sealed class HttpsStorefrontTests(HttpsStorefrontTests.HttpsFactory factory) : IClassFixture<HttpsStorefrontTests.HttpsFactory>
{
    public sealed class HttpsFactory : ApiFactory
    {
        protected override IReadOnlyDictionary<string, string?> ConfigurationOverrides => new Dictionary<string, string?>
        {
            ["Store:PublicUrl"] = "https://shop.example.com"
        };
    }

    [Fact]
    public async Task The_session_cookie_is_Secure_when_the_storefront_uses_HTTPS()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/account/register",
            new RegisterRequest($"user-{Guid.NewGuid():N}@example.com", "correct horse 42", null, "en"));

        response.EnsureSuccessStatusCode();
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(DependencyInjection.AuthCookieName + "=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
