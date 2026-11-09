using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using OnlineShopping.Api.Security;

namespace OnlineShopping.UnitTests.Api;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Every_response_carries_the_security_headers()
    {
        using var host = await StartAsync(context =>
        {
            context.Response.Headers.Server = "Kestrel";
            return context.Response.WriteAsync("ok");
        });

        using var response = await host.GetTestClient().GetAsync(new Uri("/anything", UriKind.Relative));

        foreach (var (name, value) in SecurityHeadersMiddleware.Headers)
            Assert.Equal(value, Assert.Single(response.Headers.GetValues(name)));
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Error_responses_carry_the_headers_too()
    {
        using var host = await StartAsync(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });

        using var response = await host.GetTestClient().GetAsync(new Uri("/missing", UriKind.Relative));

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
    }

    [Fact]
    public void The_api_csp_forbids_loading_and_framing()
    {
        var csp = SecurityHeadersMiddleware.Headers["Content-Security-Policy"];

        Assert.Contains("default-src 'none'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
    }

    private static async Task<IHost> StartAsync(RequestDelegate endpoint)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseMiddleware<SecurityHeadersMiddleware>();
                    app.Run(endpoint);
                }))
            .Build();
        await host.StartAsync();
        return host;
    }
}
