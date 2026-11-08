namespace OnlineShop.Application.Notifications;

/// <summary>Whether an SMS provider is configured. SMS is optional: without it every SMS feature stays off.</summary>
public interface ISmsCapability
{
    bool IsEnabled { get; }
}

/// <summary>Default when nothing else is registered: no SMS.</summary>
public sealed class NoSmsCapability : ISmsCapability
{
    public bool IsEnabled => false;
}

/// <summary>
/// Sends SMS through the configured provider. Verification codes have their own method because Iranian
/// providers only deliver codes through pre-approved templates (Kavenegar "Verify Lookup", SMS.ir "Verify").
/// </summary>
public interface ISmsSender
{
    /// <param name="phoneE164">Recipient in E.164 form, e.g. +989121234567.</param>
    /// <param name="code">The one-time code (digits only).</param>
    /// <param name="locale">Language for providers that send free text (Twilio, development pickup).</param>
    Task SendVerificationCodeAsync(string phoneE164, string code, string locale, CancellationToken cancellationToken = default);
}

/// <summary>Thrown when the provider rejects or fails a message; the caller reports "could not send".</summary>
public sealed class SmsDeliveryException : Exception
{
    public SmsDeliveryException() { }

    public SmsDeliveryException(string message) : base(message) { }

    public SmsDeliveryException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Verification-code texts for providers that send free text.</summary>
public static class SmsTexts
{
    private static readonly Dictionary<string, string> CodeTexts = new(StringComparer.Ordinal)
    {
        ["fa"] = "کد ورود شما به {0}: {1}",
        ["en"] = "Your {0} code: {1}",
        ["hy"] = "Ձեր {0} կոդը՝ {1}",
        ["de"] = "Ihr Code für {0}: {1}",
        ["ar"] = "رمز الدخول إلى {0}: {1}"
    };

    /// <summary>
    /// The code message. The last line follows the WebOTP format ("@domain #code") so Chrome on Android can fill the
    /// code in automatically, bound to the shop's domain.
    /// </summary>
    public static string VerificationCode(string locale, string storeName, string code, string domain)
    {
        var template = CodeTexts.TryGetValue(locale, out var text) ? text : CodeTexts["en"];
        var message = string.Format(System.Globalization.CultureInfo.InvariantCulture, template, storeName, code);
        return $"{message}\n\n@{domain} #{code}";
    }
}
