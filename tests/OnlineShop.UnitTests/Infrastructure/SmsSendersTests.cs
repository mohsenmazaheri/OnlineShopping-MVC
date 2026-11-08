using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using OnlineShop.Application.Common;
using OnlineShop.Application.Notifications;
using OnlineShop.Infrastructure.Notifications;

namespace OnlineShop.UnitTests.Infrastructure;

public sealed class SmsSendersTests : IDisposable
{
    private const string Phone = "+989121234567";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"onlineshop-sms-{Guid.NewGuid():N}");
    private static readonly IOptions<StoreOptions> Store = Options.Create(new StoreOptions { Name = "My Shop", PublicUrl = "https://shop.example.com" });

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    /// <summary>Captures the outgoing request and answers with a canned response.</summary>
    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static HttpClient Client(FakeHandler handler, string baseAddress) => new(handler) { BaseAddress = new Uri(baseAddress) };

    [Fact]
    public async Task Kavenegar_uses_verify_lookup_with_the_national_number_and_template()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"return":{"status":200,"message":"ok"},"entries":[]}""");
        var options = Options.Create(new SmsOptions { Kavenegar = { ApiKey = "KEY-123", VerifyTemplate = "login" } });
        using var http = Client(handler, KavenegarSmsSender.BaseAddress);

        await new KavenegarSmsSender(http, options).SendVerificationCodeAsync(Phone, "482913", "fa");

        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("https://api.kavenegar.com/v1/KEY-123/verify/lookup.json?receptor=09121234567&token=482913&template=login",
            handler.Request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Kavenegar_errors_become_delivery_exceptions()
    {
        var options = Options.Create(new SmsOptions { Kavenegar = { ApiKey = "K", VerifyTemplate = "t" } });
        using var rejected = Client(new FakeHandler(HttpStatusCode.BadRequest, """{"return":{"status":418}}"""), KavenegarSmsSender.BaseAddress);
        using var badStatus = Client(new FakeHandler(HttpStatusCode.OK, """{"return":{"status":411}}"""), KavenegarSmsSender.BaseAddress);

        await Assert.ThrowsAsync<SmsDeliveryException>(() => new KavenegarSmsSender(rejected, options).SendVerificationCodeAsync(Phone, "1", "fa"));
        await Assert.ThrowsAsync<SmsDeliveryException>(() => new KavenegarSmsSender(badStatus, options).SendVerificationCodeAsync(Phone, "1", "fa"));
    }

    [Fact]
    public async Task SmsIr_posts_the_verify_template_with_the_api_key_header()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"status":1,"message":"ok","data":{"messageId":1}}""");
        var options = Options.Create(new SmsOptions { SmsIr = { ApiKey = "IR-KEY", VerifyTemplateId = 123456, CodeParameterName = "CODE" } });
        using var http = Client(handler, SmsIrSmsSender.BaseAddress);

        await new SmsIrSmsSender(http, options).SendVerificationCodeAsync(Phone, "482913", "fa");

        Assert.Equal("https://api.sms.ir/v1/send/verify", handler.Request!.RequestUri!.AbsoluteUri);
        Assert.Equal("IR-KEY", handler.Request.Headers.GetValues("X-API-KEY").Single());
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("09121234567", body.RootElement.GetProperty("mobile").GetString());
        Assert.Equal(123456, body.RootElement.GetProperty("templateId").GetInt32());
        var parameter = body.RootElement.GetProperty("parameters")[0];
        Assert.Equal(("CODE", "482913"), (parameter.GetProperty("name").GetString(), parameter.GetProperty("value").GetString()));
    }

    [Fact]
    public async Task SmsIr_status_other_than_one_is_a_failure()
    {
        var options = Options.Create(new SmsOptions { SmsIr = { ApiKey = "K", VerifyTemplateId = 1 } });
        using var http = Client(new FakeHandler(HttpStatusCode.OK, """{"status":0,"message":"invalid template"}"""), SmsIrSmsSender.BaseAddress);

        await Assert.ThrowsAsync<SmsDeliveryException>(() => new SmsIrSmsSender(http, options).SendVerificationCodeAsync(Phone, "1", "fa"));
    }

    [Fact]
    public async Task Twilio_posts_the_localized_text_with_basic_auth()
    {
        var handler = new FakeHandler(HttpStatusCode.Created, "{}");
        var options = Options.Create(new SmsOptions { Twilio = { AccountSid = "AC1", AuthToken = "secret", From = "+15005550006" } });
        using var http = Client(handler, TwilioSmsSender.BaseAddress);

        await new TwilioSmsSender(http, options, Store).SendVerificationCodeAsync("+37491234567", "482913", "en");

        Assert.Equal("https://api.twilio.com/2010-04-01/Accounts/AC1/Messages.json", handler.Request!.RequestUri!.AbsoluteUri);
        Assert.Equal("Basic", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("AC1:secret", Encoding.UTF8.GetString(Convert.FromBase64String(handler.Request.Headers.Authorization.Parameter!)));
        var form = handler.RequestBody!.Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
        Assert.Equal("+37491234567", form["To"]);
        Assert.Equal("+15005550006", form["From"]);
        Assert.Contains("482913", form["Body"], StringComparison.Ordinal);
        Assert.EndsWith("@shop.example.com #482913", form["Body"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Twilio_can_send_through_a_messaging_service()
    {
        var handler = new FakeHandler(HttpStatusCode.Created, "{}");
        var options = Options.Create(new SmsOptions { Twilio = { AccountSid = "AC1", AuthToken = "t", MessagingServiceSid = "MG9" } });
        using var http = Client(handler, TwilioSmsSender.BaseAddress);

        await new TwilioSmsSender(http, options, Store).SendVerificationCodeAsync("+37491234567", "1", "en");

        Assert.Contains("MessagingServiceSid=MG9", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("From=", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pickup_writes_the_message_with_the_WebOTP_line()
    {
        var options = Options.Create(new SmsOptions { Provider = SmsProvider.Pickup, PickupDirectory = _directory });

        await new PickupSmsSender(options, Store, new FakeTimeProvider()).SendVerificationCodeAsync(Phone, "482913", "fa");

        var text = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(_directory)));
        Assert.Contains("To: +989121234567", text, StringComparison.Ordinal);
        Assert.Contains("My Shop", text, StringComparison.Ordinal);
        Assert.Contains("@shop.example.com #482913", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SmsProvider.None, true)]
    [InlineData(SmsProvider.Pickup, true)]
    [InlineData(SmsProvider.Kavenegar, false)]
    [InlineData(SmsProvider.SmsIr, false)]
    [InlineData(SmsProvider.Twilio, false)]
    public void Providers_without_credentials_fail_validation_at_startup(SmsProvider provider, bool valid)
    {
        var options = new SmsOptions { Provider = provider };
        var results = new List<ValidationResult>();

        Assert.Equal(valid, Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true));
    }

    [Theory]
    [InlineData(SmsProvider.None, false)]
    [InlineData(SmsProvider.Pickup, true)]
    [InlineData(SmsProvider.Kavenegar, true)]
    public void SMS_is_enabled_for_any_provider_but_None(SmsProvider provider, bool enabled)
    {
        Assert.Equal(enabled, new ConfiguredSmsCapability(Options.Create(new SmsOptions { Provider = provider })).IsEnabled);
    }

    [Fact]
    public async Task The_disabled_sender_refuses_loudly()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DisabledSmsSender().SendVerificationCodeAsync(Phone, "1", "fa"));
    }

    [Theory]
    [InlineData("fa", "کد ورود")]
    [InlineData("hy", "կոդը")]
    [InlineData("xx", "Your")]
    public void Code_texts_are_localized_with_english_fallback(string locale, string expected)
    {
        Assert.Contains(expected, SmsTexts.VerificationCode(locale, "S", "123456", "s.io"), StringComparison.Ordinal);
    }
}
