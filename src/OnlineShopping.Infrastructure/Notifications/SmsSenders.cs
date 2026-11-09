using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OnlineShopping.Application.Common;
using OnlineShopping.Application.Notifications;

namespace OnlineShopping.Infrastructure.Notifications;

/// <summary>SMS is on unless the provider is <see cref="SmsProvider.None"/>.</summary>
public sealed class ConfiguredSmsCapability(IOptions<SmsOptions> options) : ISmsCapability
{
    public bool IsEnabled => options.Value.Provider != SmsProvider.None;
}

internal static class IranianNumbers
{
    /// <summary>Iranian providers expect the national form (09121234567) rather than E.164 (+989121234567).</summary>
    public static string ToNational(string phoneE164) =>
        phoneE164.StartsWith("+98", StringComparison.Ordinal) ? "0" + phoneE164[3..] : phoneE164.TrimStart('+');
}

/// <summary>Development/testing: writes each message to a text file (and sends nothing).</summary>
public sealed class PickupSmsSender(IOptions<SmsOptions> options, IOptions<StoreOptions> store, TimeProvider clock) : ISmsSender
{
    public async Task SendVerificationCodeAsync(string phoneE164, string code, string locale, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetFullPath(options.Value.PickupDirectory);
        Directory.CreateDirectory(directory);
        var text = SmsTexts.VerificationCode(locale, store.Value.Name, code, new Uri(store.Value.PublicUrl).Host);
        var file = Path.Combine(directory, $"{clock.GetUtcNow():yyyyMMdd-HHmmss-fff}-{phoneE164.TrimStart('+')}.txt");
        await File.WriteAllTextAsync(file, $"To: {phoneE164}\n\n{text}\n", Encoding.UTF8, cancellationToken);
    }
}

/// <summary>
/// Kavenegar Verify Lookup: GET /v1/{apiKey}/verify/lookup.json?receptor=&amp;token=&amp;template=.
/// The API key is part of the URL, so HttpClient request logging must stay off (see Serilog overrides).
/// </summary>
public sealed class KavenegarSmsSender(HttpClient http, IOptions<SmsOptions> options) : ISmsSender
{
    public const string BaseAddress = "https://api.kavenegar.com/";

    public async Task SendVerificationCodeAsync(string phoneE164, string code, string locale, CancellationToken cancellationToken = default)
    {
        var settings = options.Value.Kavenegar;
        var url = $"v1/{Uri.EscapeDataString(settings.ApiKey!)}/verify/lookup.json" +
                  $"?receptor={Uri.EscapeDataString(IranianNumbers.ToNational(phoneE164))}" +
                  $"&token={Uri.EscapeDataString(code)}&template={Uri.EscapeDataString(settings.VerifyTemplate!)}";

        using var response = await http.GetAsync(new Uri(url, UriKind.Relative), cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new SmsDeliveryException($"Kavenegar rejected the message (HTTP {(int)response.StatusCode}).");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var status = json.RootElement.TryGetProperty("return", out var ret) && ret.TryGetProperty("status", out var s) ? s.GetInt32() : 0;
        if (status != 200)
            throw new SmsDeliveryException($"Kavenegar returned status {status}.");
    }
}

/// <summary>SMS.ir Verify: POST /v1/send/verify with the X-API-KEY header.</summary>
public sealed class SmsIrSmsSender(HttpClient http, IOptions<SmsOptions> options) : ISmsSender
{
    public const string BaseAddress = "https://api.sms.ir/";

    public async Task SendVerificationCodeAsync(string phoneE164, string code, string locale, CancellationToken cancellationToken = default)
    {
        var settings = options.Value.SmsIr;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("v1/send/verify", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                mobile = IranianNumbers.ToNational(phoneE164),
                templateId = settings.VerifyTemplateId,
                parameters = new[] { new { name = settings.CodeParameterName, value = code } }
            })
        };
        request.Headers.Add("X-API-KEY", settings.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new SmsDeliveryException($"SMS.ir rejected the message (HTTP {(int)response.StatusCode}).");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var status = json.RootElement.TryGetProperty("status", out var s) ? s.GetInt32() : 0;
        if (status != 1)
            throw new SmsDeliveryException($"SMS.ir returned status {status}.");
    }
}

/// <summary>Twilio Messages API: POST /2010-04-01/Accounts/{sid}/Messages.json with basic auth.</summary>
public sealed class TwilioSmsSender(HttpClient http, IOptions<SmsOptions> options, IOptions<StoreOptions> store) : ISmsSender
{
    public const string BaseAddress = "https://api.twilio.com/";

    public async Task SendVerificationCodeAsync(string phoneE164, string code, string locale, CancellationToken cancellationToken = default)
    {
        var settings = options.Value.Twilio;
        var body = SmsTexts.VerificationCode(locale, store.Value.Name, code, new Uri(store.Value.PublicUrl).Host);

        var form = new Dictionary<string, string> { ["To"] = phoneE164, ["Body"] = body };
        if (!string.IsNullOrWhiteSpace(settings.MessagingServiceSid))
            form["MessagingServiceSid"] = settings.MessagingServiceSid;
        else
            form["From"] = settings.From!;

        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri($"2010-04-01/Accounts/{Uri.EscapeDataString(settings.AccountSid!)}/Messages.json", UriKind.Relative))
        {
            Content = new FormUrlEncodedContent(form)
        };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.AccountSid}:{settings.AuthToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new SmsDeliveryException($"Twilio rejected the message (HTTP {(int)response.StatusCode}).");
    }
}

/// <summary>Used when SMS is off; reaching it means a caller forgot to check <see cref="ISmsCapability"/>.</summary>
public sealed class DisabledSmsSender : ISmsSender
{
    public Task SendVerificationCodeAsync(string phoneE164, string code, string locale, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("SMS is not configured (Sms:Provider is None).");
}
