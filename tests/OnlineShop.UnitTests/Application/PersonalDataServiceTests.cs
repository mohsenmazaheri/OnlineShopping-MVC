using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OnlineShop.Application.Accounts;
using OnlineShop.Application.Common;
using OnlineShop.Application.Customers;
using OnlineShop.Domain.Accounts;
using OnlineShop.Domain.Sales;
using OnlineShop.Infrastructure.Identity;
using OnlineShop.Infrastructure.Persistence;
using OnlineShop.UnitTests.TestSupport;

namespace OnlineShop.UnitTests.Application;

public sealed class PersonalDataServiceTests : IDisposable
{
    private const string Phone = "+989121234567";
    private readonly SqliteTestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly FakeAccountIdentity _accounts = new();
    private readonly FakeAccountDataIdentity _data;
    private readonly AddressService _addresses;
    private readonly PersonalDataService _service;
    private readonly string _password;
    private readonly string _phoneOnly;

    public PersonalDataServiceTests()
    {
        _db = _database.CreateContext();
        _data = new FakeAccountDataIdentity(_accounts);
        _addresses = new AddressService(_db, new PhoneNumberNormalizer(), Options.Create(new StoreOptions { Locales = "fa", PhoneRegion = "IR" }));
        _service = new PersonalDataService(_accounts, _data, _db, _addresses, _database.Clock, Options.Create(new AuthOptions()));

        var withPassword = TestData.User("ali@test.local");
        var phoneOnly = TestData.User("phone@test.local");
        _db.Users.AddRange(withPassword, phoneOnly);
        _db.SaveChanges();
        _password = withPassword.Id;
        _phoneOnly = phoneOnly.Id;
        _accounts.Users.Add(new FakeAccountIdentity.User(_password, "ali@test.local", null, "secret password 1", "fa"));
        _accounts.Users.Add(new FakeAccountIdentity.User(_phoneOnly, null, Phone, "", "fa"));
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private DateTimeOffset Now => _database.Clock.GetUtcNow();

    private async Task SeedAsync(string userId)
    {
        await _addresses.CreateAsync(userId, new AddressInput(null, "Ali", "09121234567", "IR", null, "Tehran", "Valiasr St", null, null, false));
        var product = await TestData.SaveProductAsync(_db, sku: $"SKU-{userId[..8]}");
        _db.Carts.Add(new Cart { CustomerId = userId, Items = { new CartItem { ProductId = product.Id, Quantity = 2 } } });
        _db.Orders.Add(new Order
        {
            CustomerId = userId, OrderedUtc = Now.UtcDateTime, Subtotal = 10, Total = 10, Status = OrderStatus.Pending,
            Items = { new OrderItem { ProductId = product.Id, ProductName = "Phone X", UnitPrice = 5, Quantity = 2 } }
        });
        _db.PhoneVerificationCodes.Add(new PhoneVerificationCode
        {
            PhoneNumber = Phone, Purpose = PhoneCodePurpose.VerifyPhone, UserId = userId, CodeHash = "h", Salt = "s",
            ExpiresUtc = Now.UtcDateTime
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_export_holds_account_addresses_orders_and_cart()
    {
        await SeedAsync(_password);

        var export = (await _service.ExportAsync(_password)).Value!;

        Assert.Equal(Now.UtcDateTime, export.ExportedUtc);
        Assert.Equal("ali@test.local", export.Account.Email);
        Assert.Equal("Tehran", Assert.Single(export.Addresses).City);
        var order = Assert.Single(export.Orders);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(new OrderItemData("Phone X", 5, 2), Assert.Single(order.Items));
        Assert.Equal(new CartItemData("Phone X", 2), Assert.Single(export.Cart));
    }

    [Fact]
    public async Task The_export_never_contains_other_customers_data()
    {
        await SeedAsync(_phoneOnly);

        var export = (await _service.ExportAsync(_password)).Value!;

        Assert.Empty(export.Addresses);
        Assert.Empty(export.Orders);
        Assert.Empty(export.Cart);
    }

    [Fact]
    public async Task Deleting_erases_personal_data_but_keeps_orders()
    {
        await SeedAsync(_password);

        var result = await _service.DeleteAsync(_password, new DeleteAccountRequest("secret password 1"), signedInAt: null);

        Assert.True(result.IsSuccess);
        Assert.Equal([_password], _data.Erased);
        await using var check = _database.CreateContext();
        Assert.False(await check.CustomerAddresses.IgnoreQueryFilters().AnyAsync());
        Assert.False(await check.Carts.IgnoreQueryFilters().AnyAsync());
        Assert.False(await check.CartItems.IgnoreQueryFilters().AnyAsync());
        Assert.False(await check.PhoneVerificationCodes.AnyAsync());
        Assert.Equal(1, await check.Orders.CountAsync(o => o.CustomerId == _password));
    }

    [Fact]
    public async Task Password_accounts_must_confirm_with_the_password()
    {
        var wrong = await _service.DeleteAsync(_password, new DeleteAccountRequest("nope"), signedInAt: Now);
        var missing = await _service.DeleteAsync(_password, new DeleteAccountRequest(null), signedInAt: Now);

        Assert.Equal(ProfileErrors.PasswordIncorrect, wrong.Error!.Code);
        Assert.Equal(ProfileErrors.PasswordIncorrect, missing.Error!.Code);
        Assert.Empty(_data.Erased);
    }

    [Fact]
    public async Task Password_less_accounts_need_a_recent_sign_in()
    {
        var old = await _service.DeleteAsync(_phoneOnly, new DeleteAccountRequest(null), signedInAt: Now.AddMinutes(-11));
        var unknown = await _service.DeleteAsync(_phoneOnly, new DeleteAccountRequest(null), signedInAt: null);

        Assert.Equal(ProfileErrors.ReauthenticationRequired, old.Error!.Code);
        Assert.Equal(ErrorKind.Forbidden, old.Error.Kind);
        Assert.Equal(ProfileErrors.ReauthenticationRequired, unknown.Error!.Code);
        Assert.Empty(_data.Erased);

        Assert.True((await _service.DeleteAsync(_phoneOnly, new DeleteAccountRequest(null), signedInAt: Now.AddMinutes(-9))).IsSuccess);
        Assert.Equal([_phoneOnly], _data.Erased);
    }

    [Fact]
    public async Task Staff_cannot_delete_themselves()
    {
        _accounts.StaffUserIds.Add(_password);

        var result = await _service.DeleteAsync(_password, new DeleteAccountRequest("secret password 1"), signedInAt: Now);

        Assert.Equal(ProfileErrors.StaffCannotDelete, result.Error!.Code);
        Assert.Equal(0, _data.PasswordChecks);
        Assert.Empty(_data.Erased);
    }

    [Fact]
    public async Task Unknown_accounts_are_not_found()
    {
        Assert.Equal(AccountErrors.NotFound, (await _service.ExportAsync("ghost")).Error!.Code);
        Assert.Equal(AccountErrors.NotFound, (await _service.DeleteAsync("ghost", new DeleteAccountRequest(null), Now)).Error!.Code);
    }
}
