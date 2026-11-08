using System.ComponentModel.DataAnnotations;

namespace OnlineShop.Application.Accounts;

/// <summary>One-time SMS code rules (section <c>PhoneCodes</c>). Defaults follow common practice for Iranian shops.</summary>
public sealed class PhoneCodeOptions
{
    public const string SectionName = "PhoneCodes";

    [Range(4, 8)]
    public int Length { get; set; } = 6;

    /// <summary>How long a code stays valid.</summary>
    [Range(30, 900)]
    public int LifetimeSeconds { get; set; } = 180;

    /// <summary>Minimum wait before another code can be sent to the same number.</summary>
    [Range(10, 600)]
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>Wrong guesses allowed per code before it is burned.</summary>
    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Codes per number per hour (limits SMS cost and abuse such as flooding someone's phone).</summary>
    [Range(1, 100)]
    public int MaxPerHour { get; set; } = 5;
}
