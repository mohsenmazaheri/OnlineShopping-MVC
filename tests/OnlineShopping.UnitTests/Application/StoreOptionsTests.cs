using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OnlineShopping.Application;
using OnlineShopping.Application.Common;

namespace OnlineShopping.UnitTests.Application;

public sealed class StoreOptionsTests
{
    [Fact]
    public void Valid_options_pass()
    {
        var options = new StoreOptions { Name = "Shop", DefaultLocale = "fa", Locales = "fa,en" };

        Assert.Empty(Validate(options));
    }

    [Fact]
    public void LocaleList_splits_and_trims_the_configured_value()
    {
        var options = new StoreOptions { Locales = " fa , en,,hy " };

        Assert.Equal(["fa", "en", "hy"], options.LocaleList);
    }

    [Fact]
    public void Default_locale_must_be_one_of_the_locales()
    {
        var options = new StoreOptions { DefaultLocale = "de", Locales = "fa,en" };

        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(StoreOptions.DefaultLocale)));
    }

    [Fact]
    public void Unsupported_locales_are_rejected()
    {
        var options = new StoreOptions { DefaultLocale = "fa", Locales = "fa,xx" };

        Assert.Contains(Validate(options), r => r.ErrorMessage!.Contains("'xx'", StringComparison.Ordinal));
    }

    [Fact]
    public void Repeated_locales_are_rejected()
    {
        var options = new StoreOptions { DefaultLocale = "fa", Locales = "fa,fa" };

        Assert.Contains(Validate(options), r => r.ErrorMessage!.Contains("repeat", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" , ")]
    public void At_least_one_locale_is_required(string locales)
    {
        var options = new StoreOptions { DefaultLocale = "fa", Locales = locales };

        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(StoreOptions.Locales)));
    }

    [Fact]
    public void A_later_configuration_source_replaces_the_whole_locale_list()
    {
        // Regression: as a configuration list, an override with fewer items kept the extra items
        // from appsettings.json (lists merge by index). As a single string it is replaced entirely.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Store:Name"] = "Shop",
                ["Store:DefaultLocale"] = "fa",
                ["Store:Locales"] = "fa,en,hy,de,ar"
            })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Store:Locales"] = "fa,en" })
            .Build();
        using var services = new ServiceCollection().AddApplication(configuration).BuildServiceProvider();

        var options = services.GetRequiredService<IOptions<StoreOptions>>().Value;

        Assert.Equal(["fa", "en"], options.LocaleList);
    }

    [Fact]
    public void Shipping_countries_default_to_the_phone_region()
    {
        Assert.Equal(["AM"], new StoreOptions { PhoneRegion = "AM" }.ShippingCountryList);
        Assert.Equal(["AM", "DE"], new StoreOptions { PhoneRegion = "AM", ShippingCountries = " AM, DE ," }.ShippingCountryList);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("DEU")]
    public void Shipping_countries_must_be_iso_codes(string countries)
    {
        var options = new StoreOptions { Locales = "fa", ShippingCountries = countries };

        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(StoreOptions.ShippingCountries)));
    }

    [Fact]
    public void Guest_checkout_is_on_by_default() => Assert.True(new StoreOptions().GuestCheckout);

    private static List<ValidationResult> Validate(StoreOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
