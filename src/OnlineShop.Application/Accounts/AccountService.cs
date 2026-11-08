using Microsoft.Extensions.Options;
using OnlineShop.Application.Common;
using OnlineShop.Application.Notifications;

namespace OnlineShop.Application.Accounts;

/// <summary>
/// Customer account use cases: register, sign in/out, confirm email, reset password.
/// Request shapes are validated before this service runs (FluentValidation endpoint filter);
/// this class enforces business rules: enabled sign-in methods, uniqueness, and no account enumeration.
/// </summary>
public sealed class AccountService(
    IAccountIdentity identity,
    LoginIdentifierParser identifierParser,
    IPhoneNumberNormalizer phoneNormalizer,
    IEmailSender emailSender,
    ISmsCapability sms,
    IOptions<StoreOptions> storeOptions,
    IOptions<AuthOptions> authOptions)
{
    private readonly StoreOptions _store = storeOptions.Value;
    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<Result<CurrentUser>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        string? phone = null;

        if (email is not null && !_auth.EmailSignUp)
            return AppError.Validation(AccountErrors.EmailSignUpDisabled, "Sign-up with email is not available.");

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            if (!_auth.PhoneSignUp || !sms.IsEnabled)
                return AppError.Validation(AccountErrors.PhoneSignUpDisabled, "Sign-up with a mobile number is not available.");
            phone = phoneNormalizer.Normalize(request.Phone, _store.PhoneRegion);
            if (phone is null)
                return AppError.Validation(AccountErrors.PhoneInvalid, "The mobile number is not valid.");
        }

        if (email is not null && await identity.EmailInUseAsync(email, cancellationToken))
            return AppError.Conflict(AccountErrors.EmailTaken, "An account with this email already exists.");
        if (phone is not null && await identity.PhoneInUseAsync(phone, cancellationToken))
            return AppError.Conflict(AccountErrors.PhoneTaken, "An account with this mobile number already exists.");

        var locale = ResolveLocale(request.Locale);
        var created = await identity.CreateCustomerAsync(email, phone, request.Password, request.DisplayName?.Trim(), locale, cancellationToken);
        if (!created.Succeeded)
            return MapCreateErrors(created.ErrorCodes);

        var userId = created.UserId!;
        if (email is not null)
            await SendConfirmationAsync(userId, email, locale, cancellationToken);

        await identity.SignInAsync(userId, rememberMe: false);
        return await CurrentUserOrNotFound(userId);
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
            SignInStatus.TwoFactorRequired => AppError.Forbidden(AccountErrors.TwoFactorRequired, "A second sign-in step is required."),
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

    private async Task SendConfirmationAsync(string userId, string email, string locale, CancellationToken cancellationToken)
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

    private string ResolveLocale(string? requested) =>
        requested is not null && _store.LocaleList.Contains(requested) ? requested : _store.DefaultLocale;

    private async Task<Result<CurrentUser>> CurrentUserOrNotFound(string userId)
    {
        var user = await identity.FindAsync(userId);
        return user is null ? AppError.NotFound(AccountErrors.NotFound, "Account not found.") : user;
    }

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
