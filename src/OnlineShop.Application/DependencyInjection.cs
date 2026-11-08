using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineShop.Application.Accounts;
using OnlineShop.Application.Catalog;
using OnlineShop.Application.Common;
using OnlineShop.Application.Customers;
using OnlineShop.Application.Notifications;

namespace OnlineShop.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StoreOptions>()
            .Bind(configuration.GetSection(StoreOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName));

        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>(includeInternalTypes: true);

        services.AddScoped<ICategoryQueries, CategoryQueries>();
        services.AddScoped<LoginIdentifierParser>();
        services.AddScoped<AccountService>();
        services.AddScoped<PhoneCodeService>();
        services.AddScoped<ExternalLoginService>();
        services.AddScoped<TwoFactorService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<PersonalDataService>();
        services.AddScoped<AddressService>();
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName));
        services.AddOptions<PhoneCodeOptions>()
            .Bind(configuration.GetSection(PhoneCodeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Replaced when an SMS provider is configured (phase 2b).
        services.TryAddSingleton<ISmsCapability, NoSmsCapability>();
        return services;
    }
}
