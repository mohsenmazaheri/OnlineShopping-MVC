using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using OnlineShop.Application.Notifications;

namespace OnlineShop.IntegrationTests.TestSupport;

public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyList<EmailMessage> Sent => [.. _sent];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>The userId and token from the link in the last email sent to <paramref name="to"/>.</summary>
    public (string UserId, string Token) LinkParametersFor(string to)
    {
        var message = Sent.Last(m => m.To == to);
        var match = LinkPattern().Match(message.TextBody);
        if (!match.Success)
            throw new InvalidOperationException($"No account link in the email to {to}.");
        return (Uri.UnescapeDataString(match.Groups["user"].Value), Uri.UnescapeDataString(match.Groups["token"].Value));
    }

    [GeneratedRegex(@"\?userId=(?<user>[^&\s]+)&token=(?<token>\S+)")]
    private static partial Regex LinkPattern();
}
