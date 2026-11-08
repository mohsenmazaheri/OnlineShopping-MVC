using OnlineShop.IntegrationTests.TestSupport;

namespace OnlineShop.UnitTests.Infrastructure;

public sealed class TotpTests
{
    // RFC 6238 appendix B: ASCII secret "12345678901234567890" (Base32 below); the 8-digit vectors end in these 6 digits.
    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(2000000000, "279037")]
    public void Matches_the_rfc_6238_test_vectors(long unixSeconds, string expected) =>
        Assert.Equal(expected, Totp.Code("GEZD GNBV GY3T QOJQ GEZD GNBV GY3T QOJQ", DateTimeOffset.FromUnixTimeSeconds(unixSeconds)));
}
