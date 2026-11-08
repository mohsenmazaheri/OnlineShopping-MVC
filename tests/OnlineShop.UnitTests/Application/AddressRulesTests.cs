using OnlineShop.Application.Accounts;
using OnlineShop.Application.Customers;

namespace OnlineShop.UnitTests.Application;

public sealed class AddressRulesTests
{
    [Theory]
    [InlineData("IR", "12345-67890", "1234567890")]
    [InlineData("IR", "۱۲۳۴۵ ۶۷۸۹۰", "1234567890")]
    [InlineData("AM", "0010", "0010")]
    [InlineData("DE", "10115", "10115")]
    [InlineData("AT", "1010", "1010")]
    [InlineData("GB", "sw1a  1aa", "SW1A 1AA")]
    public void Valid_postal_codes_are_normalized(string country, string input, string expected) =>
        Assert.Equal(expected, PostalCodes.Normalize(country, input));

    [Theory]
    [InlineData("IR", "123456789")]
    [InlineData("IR", "12345678901")]
    [InlineData("AM", "00100")]
    [InlineData("DE", "1011")]
    [InlineData("DE", "1011A")]
    [InlineData("GB", "!")]
    public void Invalid_postal_codes_are_rejected(string country, string input) =>
        Assert.Null(PostalCodes.Normalize(country, input));

    private static AddressInput Valid() =>
        new(null, "Ali", "09121234567", "IR", null, "Tehran", "Valiasr St", null, null, false);

    [Fact]
    public void A_complete_address_passes_validation() =>
        Assert.True(new AddressInputValidator().Validate(Valid()).IsValid);

    [Fact]
    public void Missing_and_too_long_fields_report_their_codes()
    {
        var input = Valid() with { RecipientName = "", City = "", Line1 = "", Phone = "", Label = new string('x', 51), CountryCode = "IRN" };

        var codes = new AddressInputValidator().Validate(input).Errors.Select(e => e.ErrorCode).ToList();

        Assert.Contains(AddressErrors.RecipientRequired, codes);
        Assert.Contains(AddressErrors.CityRequired, codes);
        Assert.Contains(AddressErrors.Line1Required, codes);
        Assert.Contains(AccountErrors.PhoneRequired, codes);
        Assert.Contains(AddressErrors.TooLong, codes);
        Assert.Contains(AddressErrors.CountryNotSupported, codes);
    }

    [Fact]
    public void Profile_and_password_requests_are_validated()
    {
        Assert.False(new UpdateProfileRequestValidator().Validate(new UpdateProfileRequest(new string('x', 101), null)).IsValid);
        Assert.True(new UpdateProfileRequestValidator().Validate(new UpdateProfileRequest("Ali", "en")).IsValid);
        Assert.Contains(new ChangePasswordRequestValidator().Validate(new ChangePasswordRequest("old", "short")).Errors,
            e => e.ErrorCode == AccountErrors.PasswordTooShort);
        Assert.True(new DeleteAccountRequestValidator().Validate(new DeleteAccountRequest(null)).IsValid);
    }
}
