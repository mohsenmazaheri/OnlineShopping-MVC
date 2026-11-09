using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration.UserSecrets;
using OnlineShopping.Api.Common;
using OnlineShopping.Api.Endpoints;
using OnlineShopping.Api.Security;
using OnlineShopping.Application;
using OnlineShopping.Infrastructure;
using OnlineShopping.Infrastructure.Persistence.Seeding;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

var isOpenApiGeneration = IsOpenApiGeneration();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, persistDataProtectionKeys: !isOpenApiGeneration);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// The API runs behind the Next.js server (and a reverse proxy); only trust forwarded headers from them.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    ReverseProxySettings.Apply(options, builder.Configuration.GetSection(ReverseProxySettings.SectionName)));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitPerWindow", 300),
                Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60)),
                QueueLimit = 0
            }));

    // Much tighter limit for sign-in, sign-up and recovery: slows down password guessing and email flooding.
    options.AddPolicy(AccountEndpoints.AuthRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitPerWindow", 10),
                Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60)),
                QueueLimit = 0
            }));
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

var app = builder.Build();

// Checked after Build: only then is all configuration in place (tests add theirs while the host is built).
// Build-time OpenAPI generation runs without a database; everything else needs a connection string.
if (!isOpenApiGeneration && ConnectionStringCheck.Problem(app.Configuration, app.Environment,
        typeof(Program).Assembly.GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId) is { } problem)
    throw new InvalidOperationException(problem);

app.UseForwardedHeaders();
app.UseMiddleware<PublicOriginMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

// TLS ends at the reverse proxy; the API is reached over the private network only, so no HTTPS redirect here.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseSerilogRequestLogging();
app.UseMiddleware<OriginCheckMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Liveness: the process answers. Readiness: the database is reachable too.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(OnlineShopping.Infrastructure.DependencyInjection.DatabaseHealthCheckTag)
}).DisableRateLimiting();

app.MapStoreEndpoints();
app.MapAccountEndpoints();
app.MapExternalLoginEndpoints();
app.MapTwoFactorEndpoints();
app.MapProfileEndpoints();
app.MapAddressEndpoints();

// Build-time OpenAPI generation starts the app without a database: skip migrations and seeding there.
if (!isOpenApiGeneration)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
}

await app.RunAsync();

static bool IsOpenApiGeneration() =>
    System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

/// <summary>Entry point marker, used by integration tests (<c>WebApplicationFactory&lt;Program&gt;</c>).</summary>
public partial class Program;
