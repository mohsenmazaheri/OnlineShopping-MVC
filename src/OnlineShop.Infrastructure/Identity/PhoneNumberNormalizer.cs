using OnlineShop.Application.Accounts;
using PhoneNumbers;

namespace OnlineShop.Infrastructure.Identity;

/// <summary>Uses Google's libphonenumber: accepts local ("0912 123 4567") and international ("+98 912…") input.</summary>
public sealed class PhoneNumberNormalizer : IPhoneNumberNormalizer
{
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    public string? Normalize(string input, string defaultRegion)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        try
        {
            var number = Util.Parse(ToAsciiDigits(input), defaultRegion);
            if (!Util.IsValidNumber(number))
                return null;

            // Accounts sign in with codes sent by SMS, so only numbers that can receive SMS are accepted.
            var type = Util.GetNumberType(number);
            if (type is not (PhoneNumberType.MOBILE or PhoneNumberType.FIXED_LINE_OR_MOBILE))
                return null;

            return Util.Format(number, PhoneNumberFormat.E164);
        }
        catch (NumberParseException)
        {
            return null;
        }
    }

    /// <summary>Persian (۰-۹) and Arabic-Indic (٠-٩) digits, as typed on Persian/Arabic keyboards, become 0-9.</summary>
    internal static string ToAsciiDigits(string input) =>
        string.Create(input.Length, input, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c switch
                {
                    >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
                    >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
                    _ => c
                };
            }
        });
}
