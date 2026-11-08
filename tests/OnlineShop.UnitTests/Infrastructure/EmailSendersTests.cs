using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MimeKit;
using OnlineShop.Application.Notifications;
using OnlineShop.Infrastructure.Notifications;

namespace OnlineShop.UnitTests.Infrastructure;

public sealed class EmailSendersTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"onlineshop-mail-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task Pickup_sender_writes_a_readable_eml_file()
    {
        var options = Options.Create(new EmailOptions { PickupDirectory = _directory, FromAddress = "shop@example.com", FromName = "Shop" });
        var sender = new PickupDirectoryEmailSender(options, new FakeTimeProvider());

        await sender.SendAsync(new EmailMessage("ali@example.com", "سلام", "متن", "<p>متن</p>"));

        var file = Assert.Single(Directory.GetFiles(_directory, "*.eml"));
        var mime = await MimeMessage.LoadAsync(file);
        Assert.Equal("سلام", mime.Subject);
        Assert.Equal("ali@example.com", mime.To.Mailboxes.Single().Address);
        Assert.Equal("shop@example.com", mime.From.Mailboxes.Single().Address);
        Assert.Contains("متن", mime.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Smtp_sender_refuses_to_run_without_a_host()
    {
        var sender = new SmtpEmailSender(Options.Create(new EmailOptions { Delivery = EmailDelivery.Smtp }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(new EmailMessage("a@b.co", "s", "t", "h")));
        Assert.Contains("SmtpHost", ex.Message, StringComparison.Ordinal);
    }
}
