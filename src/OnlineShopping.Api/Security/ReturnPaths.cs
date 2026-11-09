using OnlineShopping.Application.Common;

namespace OnlineShopping.Api.Security;

/// <summary>
/// Return addresses for browser redirects at the end of a sign-in round trip. Only paths inside the storefront and in
/// one of the store's locales are accepted, so ?returnUrl= cannot send users to another site (open redirect).
/// </summary>
public static class ReturnPaths
{
    /// <summary>The requested locale if the store offers it, else the store's default.</summary>
    public static string Locale(string? requested, StoreOptions store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return requested is not null && store.LocaleList.Contains(requested) ? requested : store.DefaultLocale;
    }

    public static string Sanitize(string? returnUrl, string locale, StoreOptions store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var home = $"/{locale}";
        if (string.IsNullOrEmpty(returnUrl) || returnUrl.Length > 2048)
            return home;
        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.Contains('\\', StringComparison.Ordinal) || returnUrl.Any(char.IsControl))
            return home;

        var firstSegment = returnUrl[1..].Split('/', '?', '#')[0];
        return store.LocaleList.Contains(firstSegment) ? returnUrl : home;
    }

    /// <summary>The login page showing an error, e.g. /fa/account/login?error=external_email_in_use.</summary>
    public static string LoginError(string locale, string errorCode) =>
        $"/{locale}/account/login?error={Uri.EscapeDataString(ShortCode(errorCode))}";

    /// <summary>The account page showing an error or a success flag.</summary>
    public static string AccountPage(string locale, string query) => $"/{locale}/account?{query}";

    /// <summary>"account.external_failed" → "external_failed" (the web app translates the short form).</summary>
    public static string ShortCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var dot = code.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? code : code[(dot + 1)..];
    }
}
