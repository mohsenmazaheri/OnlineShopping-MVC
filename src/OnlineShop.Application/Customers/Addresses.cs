using FluentValidation;

namespace OnlineShop.Application.Customers;

/// <summary>What the customer types for an address (phone in any common format; it is stored as E.164).</summary>
public sealed record AddressInput(
    string? Label,
    string RecipientName,
    string Phone,
    string CountryCode,
    string? Region,
    string City,
    string Line1,
    string? Line2,
    string? PostalCode,
    bool IsDefault);

public sealed record AddressView(
    Guid Id,
    string? Label,
    string RecipientName,
    string PhoneNumber,
    string CountryCode,
    string? Region,
    string City,
    string Line1,
    string? Line2,
    string? PostalCode,
    bool IsDefault);

/// <summary>Stable error codes for the address book (translated by the web app under <c>account.errors</c>).</summary>
public static class AddressErrors
{
    public const string NotFound = "account.address_not_found";
    public const string LimitReached = "account.address_limit_reached";
    public const string RecipientRequired = "account.address_recipient_required";
    public const string CityRequired = "account.address_city_required";
    public const string Line1Required = "account.address_line1_required";
    public const string TooLong = "account.address_too_long";
    public const string CountryNotSupported = "account.address_country_not_supported";
    public const string PostalCodeInvalid = "account.address_postal_code_invalid";
}

public sealed class AddressInputValidator : AbstractValidator<AddressInput>
{
    public AddressInputValidator()
    {
        RuleFor(a => a.RecipientName).NotEmpty().WithErrorCode(AddressErrors.RecipientRequired)
            .MaximumLength(100).WithErrorCode(AddressErrors.TooLong);
        RuleFor(a => a.Phone).NotEmpty().WithErrorCode(Accounts.AccountErrors.PhoneRequired)
            .MaximumLength(32).WithErrorCode(Accounts.AccountErrors.PhoneInvalid);
        RuleFor(a => a.CountryCode).NotEmpty().WithErrorCode(AddressErrors.CountryNotSupported)
            .Length(2).WithErrorCode(AddressErrors.CountryNotSupported);
        RuleFor(a => a.City).NotEmpty().WithErrorCode(AddressErrors.CityRequired)
            .MaximumLength(100).WithErrorCode(AddressErrors.TooLong);
        RuleFor(a => a.Line1).NotEmpty().WithErrorCode(AddressErrors.Line1Required)
            .MaximumLength(300).WithErrorCode(AddressErrors.TooLong);
        RuleFor(a => a.Line2!).MaximumLength(300).WithErrorCode(AddressErrors.TooLong).When(a => a.Line2 is not null);
        RuleFor(a => a.Region!).MaximumLength(100).WithErrorCode(AddressErrors.TooLong).When(a => a.Region is not null);
        RuleFor(a => a.Label!).MaximumLength(50).WithErrorCode(AddressErrors.TooLong).When(a => a.Label is not null);
        RuleFor(a => a.PostalCode!).MaximumLength(20).WithErrorCode(AddressErrors.PostalCodeInvalid).When(a => a.PostalCode is not null);
    }
}

/// <summary>Postal code formats of the markets the platform ships to; other countries get a loose check.</summary>
public static class PostalCodes
{
    /// <summary>Returns the normalized code (ASCII digits, upper case, single spaces) or null when it is not valid.</summary>
    public static string? Normalize(string countryCode, string postalCode)
    {
        ArgumentNullException.ThrowIfNull(postalCode);
        var code = string.Join(' ', Accounts.PhoneCodeService.NormalizeDigits(postalCode)
            .ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        // Iran: 10 digits, often written 12345-67890; stored without separators.
        if (countryCode == "IR")
            code = code.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);

        var valid = countryCode switch
        {
            "IR" => Digits(code, 10),
            "AM" or "AT" or "CH" or "BE" or "DK" or "NO" => Digits(code, 4),
            "DE" or "FR" or "ES" or "IT" or "FI" => Digits(code, 5),
            _ => code.Length is >= 2 and <= 10 && code.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-')
        };
        return valid ? code : null;
    }

    private static bool Digits(string code, int length) => code.Length == length && code.All(char.IsAsciiDigit);
}
