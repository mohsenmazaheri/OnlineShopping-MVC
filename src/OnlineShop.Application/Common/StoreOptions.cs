using System.ComponentModel.DataAnnotations;

namespace OnlineShop.Application.Common;

/// <summary>Per-installation store settings (section <c>Store</c>). Validated at startup.</summary>
public sealed class StoreOptions : IValidatableObject
{
    public const string SectionName = "Store";

    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = "Online Shop";

    /// <summary>
    /// Public address of the storefront (same as the web app's SITE_URL), e.g. https://shop.example.com.
    /// Used for links in emails and as the only origin allowed to send state-changing requests.
    /// </summary>
    [Required, Url]
    public string PublicUrl { get; set; } = "http://localhost:3000";

    /// <summary>ISO 3166-1 region used to read phone numbers typed without a country code (IR, AM, DE, …).</summary>
    [Required, RegularExpression("^[A-Z]{2}$", ErrorMessage = "Store:PhoneRegion must be a two-letter ISO country code.")]
    public string PhoneRegion { get; set; } = "IR";

    /// <summary>
    /// Countries the shop ships to (ISO 3166-1 alpha-2, comma-separated, e.g. <c>AM,DE</c>). Empty means only
    /// <see cref="PhoneRegion"/>. Read the parsed list from <see cref="ShippingCountryList"/>.
    /// </summary>
    public string ShippingCountries { get; set; } = string.Empty;

    public IReadOnlyList<string> ShippingCountryList =>
        ShippingCountries.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } list
            ? list
            : [PhoneRegion];

    /// <summary>Customers may check out without an account (on by default; checkout itself arrives in phase 5).</summary>
    public bool GuestCheckout { get; set; } = true;

    /// <summary>Origin (scheme://host[:port]) of <see cref="PublicUrl"/>.</summary>
    public string PublicOrigin => new Uri(PublicUrl).GetLeftPart(UriPartial.Authority);

    /// <summary>Locale used for <c>x-default</c> and for untranslated content.</summary>
    [Required]
    public string DefaultLocale { get; set; } = "fa";

    /// <summary>
    /// Locales this store offers, in menu order, comma-separated (e.g. <c>fa,en,hy</c>). Must contain
    /// <see cref="DefaultLocale"/>. Read the parsed list from <see cref="LocaleList"/>.
    /// </summary>
    /// <remarks>
    /// A single string on purpose. Configuration lists merge by index: overriding a 5-item list from
    /// appsettings.json with 2 items (e.g. via environment variables) would silently keep items 3–5.
    /// A string override always replaces the whole value.
    /// </remarks>
    [Required(ErrorMessage = "Configure at least one locale in Store:Locales.")]
    public string Locales { get; set; } = string.Empty;

    public IReadOnlyList<string> LocaleList =>
        Locales.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Locales the platform can render (UI translations exist for them).</summary>
    public static IReadOnlySet<string> SupportedLocales { get; } =
        new HashSet<string>(["fa", "en", "hy", "de", "ar"], StringComparer.Ordinal);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var locales = LocaleList;

        if (locales.Count == 0)
            yield return new ValidationResult("Configure at least one locale in Store:Locales.", [nameof(Locales)]);

        foreach (var locale in locales.Where(l => !SupportedLocales.Contains(l)))
            yield return new ValidationResult($"Locale '{locale}' is not supported.", [nameof(Locales)]);

        if (locales.Distinct(StringComparer.Ordinal).Count() != locales.Count)
            yield return new ValidationResult("Locales must not repeat.", [nameof(Locales)]);

        foreach (var country in ShippingCountryList.Where(c => c.Length != 2 || !c.All(char.IsAsciiLetterUpper)))
            yield return new ValidationResult($"Shipping country '{country}' must be a two-letter ISO code.", [nameof(ShippingCountries)]);

        if (!locales.Contains(DefaultLocale))
            yield return new ValidationResult(
                $"DefaultLocale '{DefaultLocale}' must be one of the store locales.", [nameof(DefaultLocale)]);
    }
}
