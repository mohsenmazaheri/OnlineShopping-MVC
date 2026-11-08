using OnlineShop.Application.Accounts;
using OnlineShop.Infrastructure.Identity;

namespace OnlineShop.UnitTests.Application;

public sealed class LoginIdentifierParserTests
{
    private readonly LoginIdentifierParser _parser = new(new PhoneNumberNormalizer());

    [Fact]
    public void Text_with_an_at_sign_is_an_email()
    {
        var identifier = _parser.Parse("  ali@example.com ", "IR");

        Assert.Equal(new LoginIdentifier("ali@example.com", null), identifier);
    }

    [Fact]
    public void Anything_else_is_read_as_a_phone_number_in_the_store_region()
    {
        Assert.Equal(new LoginIdentifier(null, "+989121234567"), _parser.Parse("09121234567", "IR"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ali@")]
    [InlineData("ali@@example.com")]
    [InlineData("12345")]
    public void Unusable_input_gives_no_identifier(string input)
    {
        Assert.Null(_parser.Parse(input, "IR"));
    }

    [Theory]
    [InlineData("a@b.co", true)]
    [InlineData("first.last+tag@shop.example.com", true)]
    [InlineData("no-at-sign.example.com", false)]
    [InlineData("@example.com", false)]
    [InlineData("a@b", false)]
    [InlineData("a@b.", false)]
    [InlineData("a b@example.com", false)]
    [InlineData("a@b@example.com", false)]
    public void Email_rules(string email, bool valid)
    {
        Assert.Equal(valid, EmailRules.IsValid(email));
    }

    [Fact]
    public void Emails_longer_than_the_RFC_limit_are_invalid()
    {
        Assert.False(EmailRules.IsValid(new string('a', 250) + "@x.io"));
    }
}
