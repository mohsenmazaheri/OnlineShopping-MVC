using Microsoft.AspNetCore.Identity;

namespace OnlineShop.Infrastructure.Identity;

/// <summary>
/// A customer or staff account. Identified by email or mobile number (at least one, both unique);
/// <see cref="IdentityUser.UserName"/> is an internal random value and never shown or used to sign in.
/// </summary>
public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }

    /// <summary>Locale for emails and SMS (one of the store locales).</summary>
    public string? PreferredLocale { get; set; }

    public DateTime CreatedUtc { get; set; }
}
