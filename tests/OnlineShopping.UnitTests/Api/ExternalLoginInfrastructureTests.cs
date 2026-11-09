using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OnlineShopping.Api.Security;
using OnlineShopping.Application.Accounts;
using OnlineShopping.Application.Common;
using OnlineShopping.Infrastructure;
using OnlineShopping.Infrastructure.Identity;
using IdentityExternalLoginInfo = Microsoft.AspNetCore.Identity.ExternalLoginInfo;
using StoreOptions = OnlineShopping.Application.Common.StoreOptions;

namespace OnlineShopping.UnitTests.Api;

public sealed class ExternalLoginInfrastructureTests
{
    private static readonly StoreOptions Store = new() { DefaultLocale = "fa", Locales = "fa,en", PublicUrl = "https://shop.example.com" };

    [Theory]
    [InlineData("/fa/account", "/fa/account")]
    [InlineData("/en/p/12/x?color=red", "/en/p/12/x?color=red")]
    [InlineData(null, "/fa")]
    [InlineData("https://evil.example/fa", "/fa")]
    [InlineData("//evil.example", "/fa")]
    [InlineData("/\\evil.example", "/fa")]
    [InlineData("/de/account", "/fa")] // locale the store does not offer
    [InlineData("/fake", "/fa")]
    public void Return_paths_stay_inside_the_storefront(string? requested, string expected)
    {
        Assert.Equal(expected, ReturnPaths.Sanitize(requested, "fa", Store));
    }

    [Fact]
    public void Locales_and_error_redirects()
    {
        Assert.Equal("en", ReturnPaths.Locale("en", Store));
        Assert.Equal("fa", ReturnPaths.Locale("de", Store));
        Assert.Equal("/fa/account/login?error=external_email_in_use", ReturnPaths.LoginError("fa", AccountErrors.ExternalEmailInUse));
        Assert.Equal("plain", ReturnPaths.ShortCode("plain"));
    }

    [Fact]
    public async Task The_API_builds_absolute_URLs_with_the_storefront_address()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(s => s.AddSingleton(Options.Create(new StoreOptions { PublicUrl = "https://shop.example.com:8443" })))
                .Configure(app =>
                {
                    app.UseMiddleware<PublicOriginMiddleware>();
                    app.Run(c => c.Response.WriteAsync($"{c.Request.Scheme}://{c.Request.Host}"));
                }))
            .StartAsync();

        Assert.Equal("https://shop.example.com:8443", await host.GetTestClient().GetStringAsync(new Uri("/x", UriKind.Relative)));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void Only_a_Google_verified_email_counts_as_verified(string? emailVerified, bool expected)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, "ali@example.com"), new(ClaimTypes.Name, "Ali") };
        if (emailVerified is not null)
            claims.Add(new Claim("email_verified", emailVerified));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var google = ExternalAuthGateway.Map(new IdentityExternalLoginInfo(principal, "Google", "g-1", "Google"))!;
        var facebook = ExternalAuthGateway.Map(new IdentityExternalLoginInfo(principal, "Facebook", "f-1", "Facebook"))!;

        Assert.Equal(new OnlineShopping.Application.Accounts.ExternalLoginInfo("google", "g-1", "ali@example.com", expected, "Ali"), google);
        Assert.False(facebook.EmailVerified); // even when a claim says so: only Google's statement is trusted
        Assert.Null(ExternalAuthGateway.Map(new IdentityExternalLoginInfo(principal, "Unknown", "k", "U")));
    }

    [Fact]
    public void Providers_are_offered_only_with_credentials()
    {
        var monitor = new StaticMonitor(new ExternalLoginOptions
        {
            Google = { ClientId = "id", ClientSecret = "secret" },
            Facebook = { ClientId = "id" } // no secret
        });

        Assert.Equal([ExternalProviders.Google], new ConfiguredExternalProviders(monitor).Enabled);
    }

    [Fact]
    public void Handlers_use_the_storefront_callback_path_and_work_without_credentials()
    {
        using var services = BuildInfrastructure(new Dictionary<string, string?>
        {
            ["Auth:External:Google:ClientId"] = "google-id",
            ["Auth:External:Google:ClientSecret"] = "google-secret",
            ["Auth:SecureCookies"] = "false"
        });
        var options = services.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get("Google");

        Assert.Equal("/api/v1/account/external/google/oauth-callback", options.CallbackPath);
        Assert.Equal("google-id", options.ClientId);
        Assert.Equal(IdentityConstants.ExternalScheme, options.SignInScheme);
        Assert.Equal(SameSiteMode.Lax, options.CorrelationCookie.SameSite);

        var facebook = services.GetRequiredService<IOptionsMonitor<Microsoft.AspNetCore.Authentication.Facebook.FacebookOptions>>().Get("Facebook");
        Assert.Equal("not-configured", facebook.AppId); // valid options, but never offered
        Assert.Equal([ExternalProviders.Google], services.GetRequiredService<IExternalLoginProviders>().Enabled);
    }

    private static ServiceProvider BuildInfrastructure(Dictionary<string, string?> settings)
    {
        settings["ConnectionStrings:Default"] = "Host=localhost;Database=never_opened;Username=test";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddApplicationAndInfrastructure(configuration);
    }

    private sealed class StaticMonitor(ExternalLoginOptions value) : IOptionsMonitor<ExternalLoginOptions>
    {
        public ExternalLoginOptions CurrentValue => value;
        public ExternalLoginOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ExternalLoginOptions, string?> listener) => null;
    }
}

internal static class TestServiceCollectionExtensions
{
    public static ServiceProvider AddApplicationAndInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        OnlineShopping.Application.DependencyInjection.AddApplication(services, configuration)
            .AddInfrastructure(configuration, persistDataProtectionKeys: false)
            .BuildServiceProvider();
}
