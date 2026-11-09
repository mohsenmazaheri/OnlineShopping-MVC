using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OnlineShopping.Application.Common;
using OnlineShopping.Application.Notifications;
using OnlineShopping.Domain.Accounts;

namespace OnlineShopping.Application.Accounts;

public sealed record PhoneCodeSent(int ExpiresInSeconds, int ResendAfterSeconds);

/// <summary>
/// Issues and checks one-time SMS codes. Codes come from a cryptographic RNG, only a salted hash is stored,
/// each code works once, expires after a few minutes and survives only a few wrong guesses; sending is limited by
/// a per-number cooldown and an hourly cap (the API's "auth" rate limit adds a per-IP cap on top).
/// </summary>
public sealed class PhoneCodeService(
    IApplicationDbContext db,
    ISmsSender sms,
    ISmsCapability smsCapability,
    IOptions<PhoneCodeOptions> options,
    TimeProvider clock)
{
    private readonly PhoneCodeOptions _options = options.Value;

    public async Task<Result<PhoneCodeSent>> SendAsync(
        string phoneE164, PhoneCodePurpose purpose, string? userId, string locale, CancellationToken cancellationToken = default)
    {
        if (!smsCapability.IsEnabled)
            return AppError.Validation(AccountErrors.SmsDisabled, "Codes by SMS are not available.");

        var now = clock.GetUtcNow().UtcDateTime;
        var recent = await db.PhoneVerificationCodes
            .Where(c => c.PhoneNumber == phoneE164 && c.CreatedUtc > now.AddHours(-1))
            .OrderByDescending(c => c.CreatedUtc)
            .Select(c => c.CreatedUtc)
            .ToListAsync(cancellationToken);

        if (recent.Count > 0)
        {
            var wait = _options.ResendCooldownSeconds - (int)(now - recent[0]).TotalSeconds;
            if (wait > 0)
                return AppError.TooManyRequests(AccountErrors.CodeCooldown, $"Wait {wait} seconds before asking for another code.");
        }
        if (recent.Count >= _options.MaxPerHour)
            return AppError.TooManyRequests(AccountErrors.CodeLimitReached, "Too many codes for this number. Try again later.");

        // Only the newest code counts: older unused codes for this number and purpose stop working.
        await db.PhoneVerificationCodes
            .Where(c => c.PhoneNumber == phoneE164 && c.Purpose == purpose && c.ConsumedUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ConsumedUtc, now), cancellationToken);
        // Housekeeping: nothing older than a day is needed for the limits above.
        await db.PhoneVerificationCodes
            .Where(c => c.PhoneNumber == phoneE164 && c.CreatedUtc < now.AddDays(-1))
            .ExecuteDeleteAsync(cancellationToken);

        var code = NewCode(_options.Length);
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        db.PhoneVerificationCodes.Add(new PhoneVerificationCode
        {
            PhoneNumber = phoneE164,
            Purpose = purpose,
            UserId = userId,
            CodeHash = Hash(code, salt),
            Salt = salt,
            CreatedUtc = now,
            ExpiresUtc = now.AddSeconds(_options.LifetimeSeconds)
        });
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await sms.SendVerificationCodeAsync(phoneE164, code, locale, cancellationToken);
        }
        catch (SmsDeliveryException)
        {
            return AppError.Validation(AccountErrors.SmsFailed, "The code could not be sent. Please try again.");
        }

        return new PhoneCodeSent(_options.LifetimeSeconds, _options.ResendCooldownSeconds);
    }

    /// <summary>Checks a code and, when correct, uses it up. <paramref name="userId"/> must match for VerifyPhone codes.</summary>
    public async Task<Result<bool>> VerifyAsync(
        string phoneE164, PhoneCodePurpose purpose, string? userId, string code, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var entry = await db.PhoneVerificationCodes
            .Where(c => c.PhoneNumber == phoneE164 && c.Purpose == purpose && c.ConsumedUtc == null)
            .OrderByDescending(c => c.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null || entry.UserId != userId)
            return AppError.Validation(AccountErrors.CodeInvalid, "The code is not correct.");
        if (entry.ExpiresUtc <= now)
            return AppError.Validation(AccountErrors.CodeExpired, "The code has expired. Ask for a new one.");

        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(entry.CodeHash),
                Encoding.ASCII.GetBytes(Hash(NormalizeDigits(code), entry.Salt))))
        {
            entry.FailedAttempts++;
            if (entry.FailedAttempts >= _options.MaxAttempts)
                entry.ConsumedUtc = now; // burned: no more guesses
            await db.SaveChangesAsync(cancellationToken);
            return entry.ConsumedUtc is null
                ? AppError.Validation(AccountErrors.CodeInvalid, "The code is not correct.")
                : AppError.Validation(AccountErrors.CodeAttemptsExceeded, "Too many wrong codes. Ask for a new one.");
        }

        entry.ConsumedUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal static string NewCode(int length)
    {
        var max = (int)Math.Pow(10, length);
        return RandomNumberGenerator.GetInt32(0, max).ToString($"D{length}", System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static string Hash(string code, string salt) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(salt + ":" + code)));

    /// <summary>Codes typed on Persian/Arabic keyboards arrive as ۱۲۳ or ١٢٣.</summary>
    internal static string NormalizeDigits(string input) =>
        new(input.Trim().Select(c => c switch
        {
            >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
            >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
            _ => c
        }).ToArray());
}
