using OnlineShop.Infrastructure.Identity;

namespace OnlineShop.UnitTests.Infrastructure;

public sealed class PhoneNumberNormalizerTests
{
    private readonly PhoneNumberNormalizer _normalizer = new();

    [Theory]
    [InlineData("09121234567", "IR", "+989121234567")]
    [InlineData("0912 123 4567", "IR", "+989121234567")]
    [InlineData("+98 912 123 4567", "DE", "+989121234567")]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷", "IR", "+989121234567")] // Persian keyboard digits
    [InlineData("٠٩١٢١٢٣٤٥٦٧", "IR", "+989121234567")] // Arabic-Indic digits
    [InlineData("091 23 45 67", "AM", "+37491234567")]
    [InlineData("0151 23456789", "DE", "+4915123456789")]
    public void Normalizes_mobile_numbers_to_E164(string input, string region, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(input, region));
    }

    [Theory]
    [InlineData("021 8888 8888", "IR")] // Tehran landline: cannot receive SMS codes
    [InlineData("12345", "IR")]
    [InlineData("not a number", "IR")]
    [InlineData("", "IR")]
    [InlineData("   ", "IR")]
    public void Rejects_numbers_that_cannot_receive_SMS(string input, string region)
    {
        Assert.Null(_normalizer.Normalize(input, region));
    }

    [Fact]
    public void Converts_Persian_and_Arabic_digits_only()
    {
        Assert.Equal("0912-abc 456", PhoneNumberNormalizer.ToAsciiDigits("۰۹۱۲-abc ٤٥٦"));
    }
}
