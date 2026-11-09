namespace OnlineShopping.Domain.Accounts;

public enum PhoneCodePurpose
{
    /// <summary>Sign in, creating the account if the number is new.</summary>
    SignIn,

    /// <summary>A signed-in user adds or changes their mobile number.</summary>
    VerifyPhone
}

/// <summary>
/// A one-time code sent by SMS. Only a salted hash of the code is stored; a code works once, for a few minutes,
/// and only for a limited number of attempts.
/// </summary>
public class PhoneVerificationCode
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>E.164, e.g. +989121234567.</summary>
    public required string PhoneNumber { get; set; }

    public PhoneCodePurpose Purpose { get; set; }

    /// <summary>For <see cref="PhoneCodePurpose.VerifyPhone"/>: the user who asked for the code.</summary>
    public string? UserId { get; set; }

    public required string CodeHash { get; set; }

    public required string Salt { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ExpiresUtc { get; set; }

    public int FailedAttempts { get; set; }

    public DateTime? ConsumedUtc { get; set; }
}
