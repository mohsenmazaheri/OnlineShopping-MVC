using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using OnlineShopping.Application.Notifications;

namespace OnlineShopping.Infrastructure.Notifications;

public enum EmailDelivery
{
    /// <summary>Write .eml files to <see cref="EmailOptions.PickupDirectory"/> (development, tests).</summary>
    Pickup,
    Smtp
}

/// <summary>Section <c>Email</c>. The SMTP password belongs in user-secrets / environment variables.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public EmailDelivery Delivery { get; set; } = EmailDelivery.Pickup;

    [Required, EmailAddress]
    public string FromAddress { get; set; } = "no-reply@localhost";

    public string? FromName { get; set; }

    public string PickupDirectory { get; set; } = "App_Data/mail";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }

    /// <summary>STARTTLS on 587 / TLS on 465. Turning it off sends passwords in clear text.</summary>
    public bool SmtpUseTls { get; set; } = true;
}

internal static class MimeMessages
{
    public static MimeMessage Create(EmailOptions options, EmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName ?? string.Empty, options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();
        return mime;
    }
}

/// <summary>Development/test delivery: each email becomes a .eml file that any mail client can open.</summary>
public sealed class PickupDirectoryEmailSender(IOptions<EmailOptions> options, TimeProvider clock) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var directory = Path.GetFullPath(options.Value.PickupDirectory);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, $"{clock.GetUtcNow():yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.eml");
        await MimeMessages.Create(options.Value, message).WriteToAsync(file, cancellationToken);
    }
}

/// <summary>Production delivery through any SMTP server (works with Iranian and international mail providers).</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
            throw new InvalidOperationException("Email:SmtpHost is required when Email:Delivery is Smtp.");

        using var client = new SmtpClient();
        var security = !settings.SmtpUseTls
            ? SecureSocketOptions.None
            : settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, security, cancellationToken);
        if (!string.IsNullOrEmpty(settings.SmtpUser))
            await client.AuthenticateAsync(settings.SmtpUser, settings.SmtpPassword ?? string.Empty, cancellationToken);
        await client.SendAsync(MimeMessages.Create(settings, message), cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
