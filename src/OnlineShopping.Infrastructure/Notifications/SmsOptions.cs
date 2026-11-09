using System.ComponentModel.DataAnnotations;

namespace OnlineShopping.Infrastructure.Notifications;

public enum SmsProvider
{
    /// <summary>No SMS: phone sign-in and phone sign-up are off.</summary>
    None,

    /// <summary>Development/testing: messages are written to text files instead of being sent.</summary>
    Pickup,

    /// <summary>Kavenegar (Iran), verification codes through a "Verify Lookup" template.</summary>
    Kavenegar,

    /// <summary>SMS.ir (Iran), verification codes through a "Verify" template.</summary>
    SmsIr,

    /// <summary>Twilio (international; not usable from Iran).</summary>
    Twilio
}

/// <summary>Section <c>Sms</c>. API keys and tokens belong in user-secrets / environment variables.</summary>
public sealed class SmsOptions : IValidatableObject
{
    public const string SectionName = "Sms";

    public SmsProvider Provider { get; set; } = SmsProvider.None;

    public string PickupDirectory { get; set; } = "App_Data/sms";

    public KavenegarSettings Kavenegar { get; set; } = new();
    public SmsIrSettings SmsIr { get; set; } = new();
    public TwilioSettings Twilio { get; set; } = new();

    public sealed class KavenegarSettings
    {
        public string? ApiKey { get; set; }

        /// <summary>Name of the Verify Lookup template created in the Kavenegar panel; its %token% receives the code.</summary>
        public string? VerifyTemplate { get; set; }
    }

    public sealed class SmsIrSettings
    {
        public string? ApiKey { get; set; }

        /// <summary>Id of the Verify template created in the SMS.ir panel.</summary>
        public int VerifyTemplateId { get; set; }

        /// <summary>Name of the template parameter that receives the code (as defined in the template).</summary>
        public string CodeParameterName { get; set; } = "Code";
    }

    public sealed class TwilioSettings
    {
        public string? AccountSid { get; set; }
        public string? AuthToken { get; set; }

        /// <summary>Sender phone number in E.164, or leave empty and set <see cref="MessagingServiceSid"/>.</summary>
        public string? From { get; set; }

        public string? MessagingServiceSid { get; set; }
    }

    /// <summary>A configured provider must have its credentials: fail at startup, not at the customer's first code.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        switch (Provider)
        {
            case SmsProvider.Kavenegar:
                if (string.IsNullOrWhiteSpace(Kavenegar.ApiKey) || string.IsNullOrWhiteSpace(Kavenegar.VerifyTemplate))
                    yield return new ValidationResult("Sms:Kavenegar:ApiKey and Sms:Kavenegar:VerifyTemplate are required.");
                break;
            case SmsProvider.SmsIr:
                if (string.IsNullOrWhiteSpace(SmsIr.ApiKey) || SmsIr.VerifyTemplateId <= 0)
                    yield return new ValidationResult("Sms:SmsIr:ApiKey and Sms:SmsIr:VerifyTemplateId are required.");
                break;
            case SmsProvider.Twilio:
                if (string.IsNullOrWhiteSpace(Twilio.AccountSid) || string.IsNullOrWhiteSpace(Twilio.AuthToken)
                    || (string.IsNullOrWhiteSpace(Twilio.From) && string.IsNullOrWhiteSpace(Twilio.MessagingServiceSid)))
                    yield return new ValidationResult("Sms:Twilio needs AccountSid, AuthToken and From or MessagingServiceSid.");
                break;
        }
    }
}
