using OnlineShop.Application.Notifications;

namespace OnlineShop.UnitTests.Application;

public sealed class EmailTemplatesTests
{
    private static readonly Uri Link = new("https://shop.example.com/fa/account/confirm-email?userId=1&token=a%2Bb");

    [Theory]
    [InlineData("fa")]
    [InlineData("en")]
    [InlineData("hy")]
    [InlineData("de")]
    [InlineData("ar")]
    public void Every_locale_has_both_templates_with_the_link(string locale)
    {
        foreach (var template in Enum.GetValues<EmailTemplate>())
        {
            var message = EmailTemplates.Render(template, locale, "en", "user@example.com", "My Shop", Link);

            Assert.Equal("user@example.com", message.To);
            Assert.Contains("My Shop", message.Subject, StringComparison.Ordinal);
            Assert.Contains(Link.AbsoluteUri, message.TextBody, StringComparison.Ordinal);
            Assert.Contains($"lang=\"{locale}\"", message.HtmlBody, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("fa", "rtl")]
    [InlineData("ar", "rtl")]
    [InlineData("de", "ltr")]
    public void Html_uses_the_text_direction_of_the_language(string locale, string dir)
    {
        var html = EmailTemplates.Render(EmailTemplate.ResetPassword, locale, "en", "u@e.co", "S", Link).HtmlBody;

        Assert.Contains($"dir=\"{dir}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_locales_fall_back_to_the_store_default()
    {
        var message = EmailTemplates.Render(EmailTemplate.ConfirmEmail, "xx", "de", "u@e.co", "S", Link);

        Assert.Contains("lang=\"de\"", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Store_names_are_html_encoded()
    {
        var html = EmailTemplates.Render(EmailTemplate.ConfirmEmail, "en", "en", "u@e.co", "<script>x</script>", Link).HtmlBody;

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }
}
