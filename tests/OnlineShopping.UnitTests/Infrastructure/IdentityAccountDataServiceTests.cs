using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineShopping.Infrastructure.Identity;
using OnlineShopping.Infrastructure.Persistence;
using OnlineShopping.UnitTests.TestSupport;

namespace OnlineShopping.UnitTests.Infrastructure;

public sealed class IdentityAccountDataServiceTests : IDisposable
{
    private const string Password = "correct horse 42";
    private readonly IdentityTestHost _host = new(s => s.AddScoped<IdentityAccountDataService>());

    public void Dispose() => _host.Dispose();

    private IdentityAccountDataService Sut => _host.Get<IdentityAccountDataService>();

    [Fact]
    public async Task The_profile_is_saved()
    {
        var user = await _host.CreateUserAsync(Password);

        await Sut.UpdateProfileAsync(user.Id, "Ali", "en");

        var saved = await _host.Users.FindByIdAsync(user.Id);
        Assert.Equal(("Ali", "en"), (saved!.DisplayName, saved.PreferredLocale));
    }

    [Fact]
    public async Task Changing_the_password_needs_the_current_one_and_renews_the_security_stamp()
    {
        var user = await _host.CreateUserAsync(Password);
        var stamp = user.SecurityStamp;

        Assert.Contains("PasswordMismatch", await Sut.ChangePasswordAsync(user.Id, "wrong", "brand new pass 7"));
        Assert.Empty(await Sut.ChangePasswordAsync(user.Id, Password, "brand new pass 7"));

        var saved = await _host.Users.FindByIdAsync(user.Id);
        Assert.True(await _host.Users.CheckPasswordAsync(saved!, "brand new pass 7"));
        Assert.NotEqual(stamp, saved!.SecurityStamp);
    }

    [Fact]
    public async Task Checking_the_password_counts_failures_towards_the_lockout()
    {
        var user = await _host.CreateUserAsync(Password);
        await _host.Users.SetLockoutEnabledAsync(user, true);

        Assert.False(await Sut.CheckPasswordAsync(user.Id, "wrong"));
        Assert.Equal(1, await _host.Users.GetAccessFailedCountAsync(user));
        Assert.True(await Sut.CheckPasswordAsync(user.Id, Password));
    }

    [Fact]
    public async Task Account_data_lists_logins_and_roles()
    {
        var user = await _host.CreateUserAsync(Password);
        await _host.Users.AddLoginAsync(user, new UserLoginInfo("Google", "g-1", "Google"));

        var data = await Sut.GetAccountDataAsync(user.Id);

        Assert.Equal(user.Email, data!.Email);
        Assert.Equal(["Google"], data.ExternalLogins);
        Assert.Empty(data.Roles);
    }

    [Fact]
    public async Task Erasing_removes_every_personal_detail_and_the_ways_to_sign_in()
    {
        var user = await _host.CreateUserAsync(Password, phone: "+989121234567");
        await _host.Users.SetAuthenticationTokenAsync(user, "[AspNetUserStore]", "AuthenticatorKey", "secret");
        await _host.Users.AddLoginAsync(user, new UserLoginInfo("Google", "g-1", "Google"));
        await Sut.UpdateProfileAsync(user.Id, "Ali", "fa");
        var stamp = (await _host.Users.FindByIdAsync(user.Id))!.SecurityStamp;

        await Sut.EraseAsync(user.Id);

        await using var db = _host.Database.CreateContext();
        var row = await db.Users.SingleAsync(u => u.Id == user.Id);
        Assert.NotNull(row.ErasedUtc);
        Assert.Null(row.Email);
        Assert.Null(row.NormalizedEmail);
        Assert.Null(row.PhoneNumber);
        Assert.Null(row.DisplayName);
        Assert.Null(row.PasswordHash);
        Assert.Equal("erased-" + user.Id, row.UserName);
        Assert.Equal(DateTimeOffset.MaxValue, row.LockoutEnd);
        Assert.NotEqual(stamp, row.SecurityStamp);
        Assert.False(await db.UserLogins.AnyAsync());
        Assert.False(await db.UserTokens.AnyAsync());
        Assert.Null(await Sut.GetAccountDataAsync(user.Id));
    }

    [Fact]
    public async Task An_erased_account_frees_its_email_and_phone_for_a_new_account()
    {
        var user = await _host.CreateUserAsync(Password, phone: "+989121234567");
        await Sut.EraseAsync(user.Id);

        var again = new AppUser { UserName = "new", Email = user.Email, PhoneNumber = "+989121234567" };

        Assert.True((await _host.Users.CreateAsync(again)).Succeeded);
    }

    [Fact]
    public async Task Erasing_twice_or_an_unknown_account_does_nothing()
    {
        var user = await _host.CreateUserAsync(Password);
        await Sut.EraseAsync(user.Id);
        await Sut.EraseAsync(user.Id);
        await Sut.EraseAsync("missing");

        await using var db = _host.Database.CreateContext();
        Assert.Equal(1, await db.Users.CountAsync());
    }
}
