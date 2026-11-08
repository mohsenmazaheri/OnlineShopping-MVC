namespace OnlineShop.Application.Accounts;

/// <summary>
/// Sign-in methods the system admin enables per deployment (section <c>Auth</c>).
/// Iran: phone + SMS, external logins off. Europe/Armenia: email first, external logins on.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Customers may create an account with an email address.</summary>
    public bool EmailSignUp { get; set; } = true;

    /// <summary>
    /// Customers may create an account with a mobile number. Takes effect only when an SMS provider is configured
    /// (phase 2b): without SMS the number could never be verified or used to reset the password.
    /// </summary>
    public bool PhoneSignUp { get; set; } = true;

    /// <summary>Send the auth cookie only over HTTPS. Turn off only for plain-HTTP test instances.</summary>
    public bool SecureCookies { get; set; } = true;

    /// <summary>Days a "remember me" sign-in stays valid (sliding).</summary>
    public int RememberMeDays { get; set; } = 14;

    /// <summary>
    /// Accounts without a password (SMS code or Google/Facebook/Microsoft sign-in) can delete themselves only this
    /// many minutes after signing in; password accounts confirm with their password instead.
    /// </summary>
    public int ReauthenticationMinutes { get; set; } = 10;
}
