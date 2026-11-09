using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OnlineShopping.Api.Common;
using OnlineShopping.Api.Security;
using OnlineShopping.Application.Common;

namespace OnlineShopping.UnitTests.Api;

public sealed class OriginCheckMiddlewareTests
{
    [Theory]
    [InlineData("POST", "https://evil.example", HttpStatusCode.Forbidden)]
    [InlineData("DELETE", "https://evil.example", HttpStatusCode.Forbidden)]
    [InlineData("POST", "https://shop.example.com", HttpStatusCode.OK)]
    [InlineData("POST", "https://SHOP.example.com/", HttpStatusCode.OK)]
    [InlineData("GET", "https://evil.example", HttpStatusCode.OK)]
    [InlineData("POST", null, HttpStatusCode.OK)]
    public async Task Only_the_storefront_origin_may_send_state_changing_requests(string method, string? origin, HttpStatusCode expected)
    {
        using var host = await StartAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/anything");
        if (origin is not null)
            request.Headers.Add("Origin", origin);

        using var response = await host.GetTestClient().SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public void Safe_methods_are_never_checked()
    {
        Assert.False(OriginCheckMiddleware.IsUnsafe("GET"));
        Assert.False(OriginCheckMiddleware.IsUnsafe("HEAD"));
        Assert.True(OriginCheckMiddleware.IsUnsafe("PATCH"));
    }

    [Fact]
    public void Validation_field_names_match_json_property_names()
    {
        Assert.Equal("newPassword", ValidationFilter<object>.JsonFieldName("NewPassword"));
        Assert.Equal("", ValidationFilter<object>.JsonFieldName(""));
    }

    private static async Task<IHost> StartAsync()
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(s => s.AddSingleton(Options.Create(new StoreOptions { PublicUrl = "https://shop.example.com" })))
                .Configure(app =>
                {
                    app.UseMiddleware<OriginCheckMiddleware>();
                    app.Run(context => context.Response.WriteAsync("ok"));
                }))
            .Build();
        await host.StartAsync();
        return host;
    }
}
