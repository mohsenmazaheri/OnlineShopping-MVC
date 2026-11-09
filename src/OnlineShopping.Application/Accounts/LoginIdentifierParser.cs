namespace OnlineShopping.Application.Accounts;

/// <summary>Decides whether what the user typed in the login box is an email address or a phone number.</summary>
public sealed class LoginIdentifierParser(IPhoneNumberNormalizer phoneNormalizer)
{
    /// <returns>The parsed identifier, or null when it is neither a plausible email nor a valid phone number.</returns>
    public LoginIdentifier? Parse(string input, string defaultRegion)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();
        if (trimmed.Contains('@', StringComparison.Ordinal))
            return EmailRules.IsValid(trimmed) ? new LoginIdentifier(trimmed, null) : null;

        var phone = phoneNormalizer.Normalize(trimmed, defaultRegion);
        return phone is null ? null : new LoginIdentifier(null, phone);
    }
}

/// <summary>Email syntax check shared by validators (deliverability is proven by the confirmation email).</summary>
public static class EmailRules
{
    public const int MaxLength = 254;

    public static bool IsValid(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > MaxLength)
            return false;

        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0
               && at == email.LastIndexOf('@')
               && at < email.Length - 3
               && email.IndexOf('.', at) > at + 1
               && !email.EndsWith('.')
               && !email.Any(char.IsWhiteSpace);
    }
}
