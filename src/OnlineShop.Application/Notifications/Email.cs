namespace OnlineShop.Application.Notifications;

public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

/// <summary>Delivers email (SMTP in production, .eml files in development). Implemented in Infrastructure.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public enum EmailTemplate
{
    ConfirmEmail,
    ResetPassword
}

/// <summary>
/// Account email texts in every supported locale. Kept short and plain so they render well in any mail client;
/// the link is the only call to action.
/// </summary>
public static class EmailTemplates
{
    private sealed record Text(string Subject, string Intro, string Action, string Ignore);

    private static readonly Dictionary<(EmailTemplate, string), Text> Texts = new()
    {
        [(EmailTemplate.ConfirmEmail, "fa")] = new("تأیید ایمیل در {0}", "برای تأیید ایمیل خود روی پیوند زیر بزنید:", "تأیید ایمیل", "اگر شما این درخواست را نداده‌اید، این پیام را نادیده بگیرید."),
        [(EmailTemplate.ConfirmEmail, "en")] = new("Confirm your email for {0}", "Please confirm your email address with the link below:", "Confirm email", "If you did not request this, you can ignore this message."),
        [(EmailTemplate.ConfirmEmail, "hy")] = new("Հաստատեք ձեր էլ. փոստը {0}-ում", "Խնդրում ենք հաստատել ձեր էլ. հասցեն ստորև նշված հղումով՝", "Հաստատել էլ. փոստը", "Եթե դուք չեք կատարել այս հարցումը, անտեսեք այս նամակը։"),
        [(EmailTemplate.ConfirmEmail, "de")] = new("Bestätigen Sie Ihre E-Mail-Adresse bei {0}", "Bitte bestätigen Sie Ihre E-Mail-Adresse über den folgenden Link:", "E-Mail bestätigen", "Wenn Sie dies nicht angefordert haben, ignorieren Sie diese Nachricht."),
        [(EmailTemplate.ConfirmEmail, "ar")] = new("تأكيد بريدك الإلكتروني في {0}", "يرجى تأكيد عنوان بريدك الإلكتروني عبر الرابط أدناه:", "تأكيد البريد الإلكتروني", "إذا لم تطلب ذلك، يمكنك تجاهل هذه الرسالة."),

        [(EmailTemplate.ResetPassword, "fa")] = new("بازیابی رمز عبور در {0}", "برای انتخاب رمز عبور جدید روی پیوند زیر بزنید. این پیوند فقط یک بار و برای مدت کوتاهی معتبر است:", "انتخاب رمز جدید", "اگر شما درخواست بازیابی رمز نداده‌اید، این پیام را نادیده بگیرید؛ رمز فعلی شما تغییری نمی‌کند."),
        [(EmailTemplate.ResetPassword, "en")] = new("Reset your password for {0}", "Use the link below to choose a new password. It works once and only for a short time:", "Choose a new password", "If you did not ask to reset your password, ignore this message; your password stays the same."),
        [(EmailTemplate.ResetPassword, "hy")] = new("Գաղտնաբառի վերականգնում {0}-ում", "Օգտագործեք ստորև նշված հղումը նոր գաղտնաբառ ընտրելու համար։ Այն գործում է մեկ անգամ և կարճ ժամանակով՝", "Ընտրել նոր գաղտնաբառ", "Եթե դուք չեք խնդրել վերականգնել գաղտնաբառը, անտեսեք այս նամակը։"),
        [(EmailTemplate.ResetPassword, "de")] = new("Passwort zurücksetzen bei {0}", "Über den folgenden Link wählen Sie ein neues Passwort. Er funktioniert nur einmal und nur kurze Zeit:", "Neues Passwort wählen", "Wenn Sie kein neues Passwort angefordert haben, ignorieren Sie diese Nachricht; Ihr Passwort bleibt unverändert."),
        [(EmailTemplate.ResetPassword, "ar")] = new("إعادة تعيين كلمة المرور في {0}", "استخدم الرابط أدناه لاختيار كلمة مرور جديدة. يعمل مرة واحدة ولفترة قصيرة فقط:", "اختيار كلمة مرور جديدة", "إذا لم تطلب إعادة تعيين كلمة المرور، تجاهل هذه الرسالة؛ ستبقى كلمة المرور كما هي.")
    };

    private static readonly HashSet<string> RtlLocales = new(["fa", "ar"], StringComparer.Ordinal);

    /// <summary>Builds the message; unknown locales fall back to <paramref name="fallbackLocale"/>.</summary>
    public static EmailMessage Render(
        EmailTemplate template, string locale, string fallbackLocale, string to, string storeName, Uri link)
    {
        ArgumentNullException.ThrowIfNull(link);

        var effectiveLocale = Texts.ContainsKey((template, locale)) ? locale : fallbackLocale;
        if (!Texts.TryGetValue((template, effectiveLocale), out var text))
        {
            effectiveLocale = "en";
            text = Texts[(template, effectiveLocale)];
        }

        var subject = string.Format(System.Globalization.CultureInfo.InvariantCulture, text.Subject, storeName);
        var url = link.AbsoluteUri;
        var textBody = $"{text.Intro}\n\n{url}\n\n{text.Ignore}\n\n— {storeName}";

        var dir = RtlLocales.Contains(effectiveLocale) ? "rtl" : "ltr";
        static string encode(string value) => System.Net.WebUtility.HtmlEncode(value);
        var htmlBody =
            $"<!doctype html><html lang=\"{effectiveLocale}\" dir=\"{dir}\"><body style=\"font-family:Tahoma,Arial,sans-serif;line-height:1.7\">" +
            $"<p>{encode(text.Intro)}</p>" +
            $"<p><a href=\"{encode(url)}\" style=\"display:inline-block;padding:10px 18px;background:#0f766e;color:#ffffff;border-radius:8px;text-decoration:none\">{encode(text.Action)}</a></p>" +
            $"<p style=\"color:#6b6b76;font-size:13px\">{encode(text.Ignore)}</p>" +
            $"<p>— {encode(storeName)}</p></body></html>";

        return new EmailMessage(to, subject, textBody, htmlBody);
    }
}
