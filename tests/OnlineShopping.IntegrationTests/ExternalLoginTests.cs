using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OnlineShopping.Api.Endpoints;
using OnlineShopping.Application.Accounts;
using OnlineShopping.IntegrationTests.TestSupport;

namespace OnlineShopping.IntegrationTests;

/// <summary>
/// The parts of external sign-in that run without a real provider: configuration, the redirect to the provider,
/// failure handling and access rules. (The provider round trip itself needs real Google/Facebook/Microsoft apps.)
/// </summary>
public sealed class ExternalLoginTests(ExternalLoginTests.GoogleFactory factory) : IClassFixture<ExternalLoginTests.GoogleFactory>
{
    public sealed class GoogleFactory : ApiFactory
    {
        protected override IReadOnlyDictionary<string, string?> ConfigurationOverrides => new Dictionary<string, string?>
        {
            ["Auth:External:Google:ClientId"] = "test-client-id.apps.googleusercontent.com",
            ["Auth:External:Google:ClientSecret"] = "test-secret"
        };
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HttpClient NoRedirectClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Store_info_lists_only_configured_providers()
    {
        using var client = factory.CreateClient();

        var store = await client.GetFromJsonAsync<StoreInfoResponse>("/api/v1/store", Json);

        Assert.Equal([ExternalProviders.Google], store!.SignIn.External);
    }

    [Fact]
    public async Task Starting_Google_sign_in_redirects_to_Google_with_the_storefront_callback()
    {
        using var client = NoRedirectClient();

        using var response = await client.GetAsync("/api/v1/account/external/google/start?returnUrl=%2Ffa%2Faccount&locale=fa");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("accounts.google.com", location.Host);
        var query = System.Web.HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("test-client-id.apps.googleusercontent.com", query["client_id"]);
        // Built from Store:PublicUrl, not the API's own address.
        Assert.Equal($"{ApiFactory.PublicUrl}/api/v1/account/external/google/oauth-callback", query["redirect_uri"]);
        Assert.False(string.IsNullOrEmpty(query["state"]));

        var correlation = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Correlation", StringComparison.Ordinal));
        Assert.Contains("samesite=lax", correlation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", correlation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Providers_without_credentials_do_not_exist()
    {
        using var client = NoRedirectClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/account/external/facebook/start")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/account/external/unknown/start")).StatusCode);
    }

    [Fact]
    public async Task A_forged_or_cancelled_provider_response_returns_to_the_login_page()
    {
        using var client = NoRedirectClient();

        using var response = await client.GetAsync("/api/v1/account/external/google/oauth-callback?state=forged&code=x");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/fa/account/login?error=external_failed", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task The_callback_without_a_provider_round_trip_signs_nobody_in()
    {
        using var client = NoRedirectClient();

        using var response = await client.GetAsync("/api/v1/account/external/callback?returnUrl=%2Ffa%2Faccount&locale=en");

        Assert.Equal("/en/account/login?error=external_failed", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/me")).StatusCode);
    }

    [Fact]
    public async Task Linked_logins_and_linking_need_a_session()
    {
        using var client = NoRedirectClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/external")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/external/google/link")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/account/external/google/unlink", null)).StatusCode);
    }

    [Fact]
    public async Task A_signed_in_user_sees_available_and_linked_providers_and_can_start_linking()
    {
        using var client = NoRedirectClient();
        (await client.PostAsJsonAsync("/api/v1/account/register",
            new RegisterRequest($"user-{Guid.NewGuid():N}@example.com", "correct horse 42", null, "en"))).EnsureSuccessStatusCode();

        var view = await client.GetFromJsonAsync<ExternalLoginsView>("/api/v1/account/external", Json);
        Assert.Equal([ExternalProviders.Google], view!.Available);
        Assert.Empty(view.Linked);

        using var link = await client.GetAsync("/api/v1/account/external/google/link?locale=en");
        Assert.Equal(HttpStatusCode.Redirect, link.StatusCode);
        Assert.Equal("accounts.google.com", link.Headers.Location!.Host);

        using var unlink = await client.PostAsync("/api/v1/account/external/google/unlink", null);
        Assert.Equal(HttpStatusCode.NotFound, unlink.StatusCode);
    }

    [Fact]
    public async Task Return_addresses_outside_the_storefront_are_ignored()
    {
        using var client = NoRedirectClient();

        using var response = await client.GetAsync("/api/v1/account/external/google/start?returnUrl=https%3A%2F%2Fevil.example&locale=fa");

        // The return address travels inside the encrypted state, so check what the callback will use.
        var state = System.Web.HttpUtility.ParseQueryString(response.Headers.Location!.Query)["state"];
        Assert.False(string.IsNullOrEmpty(state));
        Assert.DoesNotContain("evil.example", response.Headers.Location.OriginalString, StringComparison.Ordinal);
    }
}
