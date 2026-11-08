using Microsoft.Extensions.Options;
using OnlineShop.Application.Accounts;
using OnlineShop.Application.Common;
using OnlineShop.UnitTests.TestSupport;

namespace OnlineShop.UnitTests.Application;

public sealed class TwoFactorServiceTests
{
    private const string Valid = FakeTwoFactorIdentity.ValidCode;

    private readonly FakeAccountIdentity _identity = new();
    private readonly FakeTwoFactorIdentity _twoFactor = new();

    public TwoFactorServiceTests() =>
        _identity.Users.Add(new FakeAccountIdentity.User("u1", "ali@example.com", null, "correct horse 42", "fa"));

    private TwoFactorService Create(bool requireStaff = false) =>
        new(_twoFactor, _identity, Options.Create(new StoreOptions { Name = "My Shop" }),
            new StaticSecurity(new SecurityOptions { RequireStaffTwoFactor = requireStaff }));

    [Fact]
    public async Task Setup_returns_a_readable_key_and_an_otpauth_uri_for_the_QR_code()
    {
        var setup = (await Create().BeginSetupAsync("u1")).Value;

        Assert.Equal("jbsw y3dp ehpk 3pxp", setup.SharedKey);
        Assert.Equal("otpauth://totp/My%20Shop:ali%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=My%20Shop&digits=6", setup.AuthenticatorUri);
    }

    [Fact]
    public async Task Enabling_needs_a_working_code_and_returns_ten_recovery_codes()
    {
        var service = Create();

        Assert.Equal(AccountErrors.CodeInvalid, (await service.EnableAsync("u1", "000000")).Error!.Code);
        Assert.DoesNotContain("u1", _twoFactor.Enabled);

        var codes = await service.EnableAsync("u1", " ۱۲۳ ۴۵۶ "); // spaces and Persian digits are fine
        Assert.Equal(10, codes.Value.Codes.Count);
        Assert.Contains("u1", _twoFactor.Enabled);
        Assert.Equal(AccountErrors.TwoFactorAlreadyEnabled, (await service.BeginSetupAsync("u1")).Error!.Code);
    }

    [Fact]
    public async Task Turning_it_off_needs_a_current_code()
    {
        _twoFactor.Enabled.Add("u1");
        var service = Create();

        Assert.Equal(AccountErrors.CodeInvalid, (await service.DisableAsync("u1", "000000")).Error!.Code);
        Assert.Contains("u1", _twoFactor.Enabled);

        Assert.True((await service.DisableAsync("u1", Valid)).IsSuccess);
        Assert.DoesNotContain("u1", _twoFactor.Enabled);
    }

    [Fact]
    public async Task Staff_cannot_turn_it_off_while_it_is_required()
    {
        _identity.StaffUserIds.Add("u1");
        _twoFactor.Enabled.Add("u1");
        var service = Create(requireStaff: true);

        var result = await service.DisableAsync("u1", Valid);

        Assert.Equal(AccountErrors.TwoFactorRequiredForStaff, result.Error!.Code);
        Assert.True((await service.GetStatusAsync("u1")).Value.RequiredForAccount);
    }

    [Fact]
    public async Task Customers_are_never_forced()
    {
        Assert.False((await Create(requireStaff: true).GetStatusAsync("u1")).Value.RequiredForAccount);
    }

    [Fact]
    public async Task New_recovery_codes_need_a_current_code_and_two_step_sign_in_on()
    {
        var service = Create();
        Assert.Equal(AccountErrors.TwoFactorNotEnabled, (await service.RegenerateRecoveryCodesAsync("u1", Valid)).Error!.Code);

        _twoFactor.Enabled.Add("u1");
        Assert.Equal(AccountErrors.CodeInvalid, (await service.RegenerateRecoveryCodesAsync("u1", "000000")).Error!.Code);
        Assert.Equal(10, (await service.RegenerateRecoveryCodesAsync("u1", Valid)).Value.Codes.Count);
    }

    [Fact]
    public async Task The_second_step_signs_in_with_the_right_code()
    {
        _twoFactor.PendingUserId = "u1";
        var service = Create();

        Assert.Equal(AccountErrors.CodeInvalid, (await service.SignInWithAuthenticatorAsync(new TwoFactorSignInRequest("000000", false, false))).Error!.Code);
        Assert.Equal("u1", (await service.SignInWithAuthenticatorAsync(new TwoFactorSignInRequest(Valid, false, false))).Value.Id);
    }

    [Fact]
    public async Task Without_a_pending_first_step_the_user_must_start_over()
    {
        var result = await Create().SignInWithAuthenticatorAsync(new TwoFactorSignInRequest(Valid, false, false));

        Assert.Equal(AccountErrors.TwoFactorSessionExpired, result.Error!.Code);
    }

    [Fact]
    public async Task A_recovery_code_works_once()
    {
        _twoFactor.PendingUserId = "u1";
        _twoFactor.Codes["u1"] = ["abcde-12345"];
        var service = Create();

        Assert.True((await service.SignInWithRecoveryCodeAsync(new RecoveryCodeSignInRequest(" abcde-12345 "))).IsSuccess);
        Assert.Equal(AccountErrors.CodeInvalid, (await service.SignInWithRecoveryCodeAsync(new RecoveryCodeSignInRequest("abcde-12345"))).Error!.Code);
    }

    [Fact]
    public void Helpers_format_keys_and_clean_codes()
    {
        Assert.Equal("abcd efgh ij", TwoFactorService.FormatKey("ABCDEFGHIJ"));
        Assert.Equal("123456", TwoFactorService.Clean("12 34-56"));
        Assert.Equal("123456", TwoFactorService.Clean("١٢٣٤٥٦"));
    }

}
