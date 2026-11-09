using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineShopping.Api.Endpoints;
using OnlineShopping.Application.Authorization;
using OnlineShopping.Application.Catalog;
using OnlineShopping.Domain.Catalog;
using OnlineShopping.Infrastructure.Persistence;
using OnlineShopping.IntegrationTests.TestSupport;

namespace OnlineShopping.IntegrationTests;

public sealed class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Startup_applies_migrations_and_seeds_every_role()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
            Assert.True(await roles.RoleExistsAsync(role), $"Role {role} missing");
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_report_healthy(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Store_info_comes_from_configuration()
    {
        using var client = factory.CreateClient();

        var store = await client.GetFromJsonAsync<StoreInfoResponse>("/api/v1/store", Json);

        Assert.NotNull(store);
        Assert.Equal("Test Shop", store.Name);
        Assert.Equal("fa", store.DefaultLocale);
        Assert.Equal(["fa", "en"], store.Locales);
        Assert.Equal(new SignUpMethods(Email: true, Phone: false), store.SignUp);
    }

    [Fact]
    public async Task Category_menu_returns_live_categories_from_PostgreSQL()
    {
        using var client = factory.CreateClient();
        var slug = $"books-{Guid.NewGuid():N}";
        await SeedAsync(
            new Category { Name = "Books", Slug = slug, SubCategories = [new SubCategory { Name = "Poetry", Slug = "poetry" }] },
            new Category { Name = "Deleted", Slug = $"deleted-{Guid.NewGuid():N}", IsDeleted = true });

        var menu = await client.GetFromJsonAsync<List<CategorySummary>>("/api/v1/catalog/categories", Json);

        var books = Assert.Single(menu!, c => c.Slug == slug);
        Assert.Equal("Poetry", Assert.Single(books.SubCategories).Name);
        Assert.DoesNotContain(menu!, c => c.Name == "Deleted");
    }

    [Fact]
    public async Task Unknown_category_returns_problem_details_with_an_error_code()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/catalog/categories/no-such-category");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(CategoryQueries.NotFoundCode, body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Responses_carry_security_headers_and_hide_the_server()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/store");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task OpenApi_document_is_not_exposed_outside_development()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Data_protection_keys_are_stored_in_the_database()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var protector = scope.ServiceProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("test");

        Assert.Equal("secret", protector.Unprotect(protector.Protect("secret")));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.NotEmpty(await db.DataProtectionKeys.ToListAsync());
    }

    private async Task SeedAsync(params Category[] categories)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Categories.AddRange(categories);
        await db.SaveChangesAsync();
    }
}
