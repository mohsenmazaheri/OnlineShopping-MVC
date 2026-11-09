using Microsoft.Extensions.Options;
using OnlineShopping.Application.Accounts;
using OnlineShopping.Application.Common;
using OnlineShopping.UnitTests.TestSupport;

namespace OnlineShopping.UnitTests.Application;

public sealed class ProfileServiceTests
{
    private readonly FakeAccountIdentity _accounts = new();
    private readonly FakeAccountDataIdentity _data;
    private readonly ProfileService _service;

    public ProfileServiceTests()
    {
        _data = new FakeAccountDataIdentity(_accounts);
        _service = new ProfileService(_accounts, _data, Options.Create(new StoreOptions { Locales = "fa,en", DefaultLocale = "fa" }));
        _accounts.Users.Add(new FakeAccountIdentity.User("u1", "ali@example.com", null, "old password 1", "fa"));
        _accounts.Users.Add(new FakeAccountIdentity.User("phone", null, "+989121234567", "", "fa"));
    }

    [Fact]
    public async Task The_name_is_trimmed_and_the_language_saved()
    {
        var result = await _service.UpdateAsync("u1", new UpdateProfileRequest("  Ali  ", "en"));

        Assert.True(result.IsSuccess);
        Assert.Equal(("Ali", "en"), _data.Profiles["u1"]);
    }

    [Fact]
    public async Task Blank_values_clear_the_fields()
    {
        await _service.UpdateAsync("u1", new UpdateProfileRequest(" ", ""));

        Assert.Equal((null, null), _data.Profiles["u1"]);
    }

    [Fact]
    public async Task Only_the_shop_languages_are_accepted()
    {
        var result = await _service.UpdateAsync("u1", new UpdateProfileRequest("Ali", "de"));

        Assert.Equal(ProfileErrors.LocaleNotSupported, result.Error!.Code);
        Assert.Empty(_data.Profiles);
    }

    [Fact]
    public async Task An_unknown_account_is_not_found() =>
        Assert.Equal(AccountErrors.NotFound, (await _service.UpdateAsync("ghost", new UpdateProfileRequest("x", null))).Error!.Code);

    [Fact]
    public async Task Changing_the_password_needs_the_current_one()
    {
        Assert.Equal(ProfileErrors.PasswordIncorrect,
            (await _service.ChangePasswordAsync("u1", new ChangePasswordRequest("wrong", "new password 22"))).Error!.Code);

        Assert.True((await _service.ChangePasswordAsync("u1", new ChangePasswordRequest("old password 1", "new password 22"))).IsSuccess);
        Assert.Equal("new password 22", _accounts.Users.Single(u => u.Id == "u1").CurrentPassword);
    }

    [Fact]
    public async Task Identity_password_rules_are_reported_as_rejected() =>
        Assert.Equal(AccountErrors.PasswordRejected,
            (await _service.ChangePasswordAsync("u1", new ChangePasswordRequest("old password 1", "short1"))).Error!.Code);

    [Fact]
    public async Task Password_less_accounts_have_nothing_to_change() =>
        Assert.Equal(ProfileErrors.NoPassword,
            (await _service.ChangePasswordAsync("phone", new ChangePasswordRequest("x", "new password 22"))).Error!.Code);
}
