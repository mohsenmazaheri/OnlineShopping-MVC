using Microsoft.Extensions.Options;
using OnlineShop.Application.Common;
using OnlineShop.Application.Notifications;
using OnlineShop.Domain.Accounts;

namespace OnlineShop.Application.Accounts;

/// <summary>
/// Customer account use cases: register, sign in/out (password or SMS code), confirm email and phone, reset password.
/// Request shapes are validated before this service runs (FluentValidation endpoint filter);
/// this class enforces business rules: enabled sign-in methods, uniqueness, and no account enumeration.
/// </summary>
public sealed class AccountService(
    IAccountIdentity identity,
    LoginIdentifierParser identifierParser,
    IPhoneNumberNormalizer phoneNormalizer,
    PhoneCodeService phoneCodes,
    IEmailSender emailSender,
    IOptions<StoreOptions> storeOptions,
    IOptions<AuthOptions> authOptions)
{
    private readonly StoreOptions _store = storeOptions.Value;
    private readonly AuthOptions _auth = authOptions.Value;

    /// <summary>Password sign-up with an email address.</summary>
    public async Task<Result<CurrentUser>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_auth.EmailSignUp)
            return AppError.Validation(AccountErrors.EmailSignUpDisabled, "Sign-up with email is not available.");

        var email = request.Email!.Trim();
        if (await identity.EmailInUseAsync(email, cancellationToken))
            return AppError.Conflict(AccountErrors.EmailTaken, "An account with this email already exists.");

        var locale = ResolveLocale(request.Locale);
        var created = await identity.CreateCustomerAsync(email, null, request.Password, request.DisplayName?.Trim(), locale, cancellationToken);
        if (!created.Succeeded)
            return MapCreateErrors(created.ErrorCodes);

        var userId = created.UserId!;
        await SendConfirmationAsync(userId, email, locale, cancellationToken);
        await identity.SignInAsync(userId, rememberMe: false);
        return await CurrentUserOrNotFound(userId);
    }

    /// <summary>
    /// Sends a sign-in code. The answer is the same whether or not the number has an account
    /// (the account is created on first sign-in), so it reveals nothing.
    /// </summary>
    public async Task<Result<PhoneCodeSent>> SendSignInCodeAsync(SendPhoneCodeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var phone = phoneNormalizer.Normalize(request.Phone, _store.PhoneRegion);
        if (phone is null)
            return AppError.Validation(AccountErrors.PhoneInvalid, "The mobile number is not valid.");
        return await phoneCodes.SendAsync(phone, PhoneCodePurpose.SignIn, null, ResolveLocale(request.Locale), cancellationToken);
    }

    /// <summary>Signs in with an SMS code; creates a (verified) customer account when the number is new.</summary>
    public async Task<Result<PhoneSignInResult>> SignInWithCodeAsync(PhoneSignInRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var phone = phoneNormalizer.Normalize(request.Phone, _store.PhoneRegion);
        if (phone is null)
            return AppError.Validation(AccountErrors.PhoneInvalid, "The mobile number is not valid.");

        var verified = await phoneCodes.VerifyAsync(phone, PhoneCodePurpose.SignIn, null, request.Code, cancellationToken);
        if (!verified.IsSuccess)
            return verified.Error!;

        var userId = await identity.FindUserIdByPhoneAsync(phone, cancellationToken);
        var isNew = userId is null;
        if (isNew)
        {
            if (!_auth.PhoneSignUp)
                return AppError.Validation(AccountErrors.PhoneSignUpDisabled, "Sign-up with a mobile number is not available.");
            var created = await identity.CreatePhoneCustomerAsync(phone, request.DisplayName?.Trim(), ResolveLocale(request.Locale), cancellationToken);
            if (!created.Succeeded)
                return MapCreateErrors(created.ErrorCodes);
            userId = created.UserId!;
        }
        else if (await identity.IsLockedOutAsync(userId!))
        {
            // A code proves the phone, not that the account may sign in while it is locked.
            return AppError.Forbidden(AccountErrors.LockedOut, "Too many failed attempts. Try again later.");
        }

        // An account with two-step sign-in needs the authenticator after the SMS code too.
        if (await identity.SignInAsync(userId!, request.RememberMe) == SignInStatus.TwoFactorRequired)
            return TwoFactorStepRequired();
        var user = await identity.FindAsync(userId!);
        return user is null
            ? AppError.NotFound(AccountErrors.NotFound, "Account not found.")
            : new PhoneSignInResult(user, isNew);
    }

    /// <summary>Sends a code to a number the signed-in user wants to add to their account.</summary>
    public async Task<Result<PhoneCodeSent>> SendAddPhoneCodeAsync(string userId, SendPhoneCodeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var phone = phoneNormalizer.Normalize(request.Phone, _store.PhoneRegion);
        if (phone is null)
            return AppError.Validation(AccountErrors.PhoneInvalid, "The mobile number is not valid.");

        var owner = await identity.FindUserIdByPhoneAsync(phone, cancellationToken);
        if (owner is not null && owner != userId)
            return AppError.Conflict(AccountErrors.PhoneTaken, "Another account already uses this mobile number.");

        var user = await identity.FindAsync(userId);
        return await phoneCodes.SendAsync(phone, PhoneCodePurpose.VerifyPhone, userId,
            ResolveLocale(request.Locale ?? user?.PreferredLocale), cancellationToken);
    }

    /// <summary>Saves the number as the user's verified phone once they enter the code.</summary>
    public async Task<Result<CurrentUser>> ConfirmAddPhoneAsync(string userId, ConfirmPhoneRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var phone = phoneNormalizer.Normalize(request.Phone, _store.PhoneRegion);
        if (phone is null)
            return AppError.Validation(AccountErrors.PhoneInvalid, "The mobile number is not valid.");

        var verified = await phoneCodes.VerifyAsync(phone, PhoneCodePurpose.VerifyPhone, userId, request.Code, cancellationToken);
        if (!verified.IsSuccess)
            return verified.Error!;

        var saved = await identity.SetConfirmedPhoneAsync(userId, phone);
        return saved.Succeeded ? await CurrentUserOrNotFound(userId) : MapCreateErrors(saved.ErrorCodes);
    }

    public async Task<Result<CurrentUser>> LoginAsync(LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identifier = identifierParser.Parse(request.Identifier, _store.PhoneRegion);
        // An unparseable identifier gets the same answer as a wrong password: no hint about which part was wrong.
        if (identifier is null)
            return InvalidCredentials();

        var (status, userId) = await identity.PasswordSignInAsync(identifier, request.Password, request.RememberMe);
        return status switch
        {
            SignInStatus.Succeeded => await CurrentUserOrNotFound(userId!),
            SignInStatus.LockedOut => AppError.Forbidden(AccountErrors.LockedOut, "Too many failed attempts. Try again later."),
            SignInStatus.TwoFactorRequired => TwoFactorStepRequired(),
            SignInStatus.NotAllowed => AppError.Forbidden(AccountErrors.NotAllowed, "This account cannot sign in."),
            _ => InvalidCredentials()
        };
    }

    public Task LogoutAsync() => identity.SignOutAsync();

    public async Task<Result<CurrentUser>> GetCurrentAsync(string userId) => await CurrentUserOrNotFound(userId);

    public async Task<Result<bool>> ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await identity.ConfirmEmailAsync(request.UserId, request.Token)
            ? true
            : AppError.Validation(AccountErrors.InvalidToken, "The confirmation link is invalid or has expired.");
    }

    public async Task<Result<bool>> ResendConfirmationAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await identity.FindAsync(userId);
        if (user is null)
            return AppError.NotFound(AccountErrors.NotFound, "Account not found.");
        if (user.Email is null)
            return AppError.Validation(AccountErrors.NoEmail, "This account has no email address.");
        if (user.EmailConfirmed)
            return true;

        await SendConfirmationAsync(user.Id, user.Email, ResolveLocale(user.PreferredLocale), cancellationToken);
        return true;
    }

    /// <summary>Always succeeds, whether or not the email belongs to an account (prevents account enumeration).</summary>
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reset = await identity.CreatePasswordResetTokenAsync(request.Email.Trim());
        if (reset is null)
            return;

        var (userId, token, preferredLocale) = reset.Value;
        var locale = ResolveLocale(preferredLocale ?? request.Locale);
        var link = BuildLink(locale, "account/reset-password", userId, token);
        var message = EmailTemplates.Render(EmailTemplate.ResetPassword, locale, _store.DefaultLocale, request.Email.Trim(), _store.Name, link);
        await emailSender.SendAsync(message, cancellationToken);
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await identity.ResetPasswordAsync(request.UserId, request.Token, request.NewPassword);
        if (result.Succeeded)
            return true;
        return result.ErrorCodes.Contains("InvalidToken")
            ? AppError.Validation(AccountErrors.InvalidToken, "The reset link is invalid or has expired.")
            : AppError.Validation(AccountErrors.PasswordRejected, "The new password does not meet the requirements.");
    }

    internal async Task SendConfirmationAsync(string userId, string email, string locale, CancellationToken cancellationToken)
    {
        var token = await identity.CreateEmailConfirmationTokenAsync(userId);
        if (token is null)
            return;

        var link = BuildLink(locale, "account/confirm-email", userId, token);
        var message = EmailTemplates.Render(EmailTemplate.ConfirmEmail, locale, _store.DefaultLocale, email, _store.Name, link);
        await emailSender.SendAsync(message, cancellationToken);
    }

    /// <summary>Links always point at the storefront (never the API host), in the user's language.</summary>
    internal Uri BuildLink(string locale, string path, string userId, string token) =>
        new($"{_store.PublicUrl.TrimEnd('/')}/{locale}/{path}?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}");

    internal string ResolveLocale(string? requested) =>
        requested is not null && _store.LocaleList.Contains(requested) ? requested : _store.DefaultLocale;

    private async Task<Result<CurrentUser>> CurrentUserOrNotFound(string userId)
    {
        var user = await identity.FindAsync(userId);
        return user is null ? AppError.NotFound(AccountErrors.NotFound, "Account not found.") : user;
    }

    internal static AppError TwoFactorStepRequired() =>
        AppError.Forbidden(AccountErrors.TwoFactorRequired, "A second sign-in step is required.");

    private static AppError InvalidCredentials() =>
        AppError.Validation(AccountErrors.InvalidCredentials, "The sign-in details are not correct.");

    private static AppError MapCreateErrors(IReadOnlyList<string> codes)
    {
        if (codes.Contains("DuplicateEmail"))
            return AppError.Conflict(AccountErrors.EmailTaken, "An account with this email already exists.");
        if (codes.Contains("DuplicatePhoneNumber"))
            return AppError.Conflict(AccountErrors.PhoneTaken, "An account with this mobile number already exists.");
        return AppError.Validation(AccountErrors.PasswordRejected, "The password does not meet the requirements.");
    }
}
