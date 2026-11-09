using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OnlineShopping.Application.Accounts;
using OnlineShopping.Application.Common;
using OnlineShopping.Application.Customers;
using OnlineShopping.Domain.Customers;
using OnlineShopping.Infrastructure.Identity;
using OnlineShopping.Infrastructure.Persistence;
using OnlineShopping.UnitTests.TestSupport;

namespace OnlineShopping.UnitTests.Application;

public sealed class AddressServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AddressService _service;
    private readonly string _ali;
    private readonly string _sara;

    public AddressServiceTests()
    {
        _db = _database.CreateContext();
        var store = new StoreOptions { Locales = "fa,en", PhoneRegion = "IR", ShippingCountries = "IR,AM" };
        _service = new AddressService(_db, new PhoneNumberNormalizer(), Options.Create(store));

        var ali = TestData.User("ali@test.local");
        var sara = TestData.User("sara@test.local");
        _db.Users.AddRange(ali, sara);
        _db.SaveChanges();
        _ali = ali.Id;
        _sara = sara.Id;
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private static AddressInput Input(string city = "Tehran", bool isDefault = false, string country = "IR",
        string phone = "۰۹۱۲۱۲۳۴۵۶۷", string? postalCode = "12345-67890") =>
        new(" Home ", "  Ali Rezaei ", phone, country, "Tehran", city, "Valiasr St, No. 10", "  ", postalCode, isDefault);

    private async Task<AddressView> CreateAsync(string userId, string city = "Tehran", bool isDefault = false)
    {
        var result = await _service.CreateAsync(userId, Input(city, isDefault));
        Assert.True(result.IsSuccess, result.Error?.Code);
        return result.Value!;
    }

    [Fact]
    public async Task Creating_normalizes_the_input_and_makes_the_first_address_the_default()
    {
        var address = await CreateAsync(_ali);

        Assert.True(address.IsDefault);
        Assert.Equal("Home", address.Label);
        Assert.Equal("Ali Rezaei", address.RecipientName);
        Assert.Equal("+989121234567", address.PhoneNumber);
        Assert.Equal("1234567890", address.PostalCode);
        Assert.Null(address.Line2);
    }

    [Fact]
    public async Task A_new_default_replaces_the_old_one()
    {
        var first = await CreateAsync(_ali, "Tehran");
        var second = await CreateAsync(_ali, "Shiraz", isDefault: true);
        await CreateAsync(_ali, "Tabriz");

        var list = await _service.ListAsync(_ali);

        Assert.Equal(second.Id, list[0].Id);
        Assert.Single(list, a => a.IsDefault);
        Assert.False(list.Single(a => a.Id == first.Id).IsDefault);
    }

    [Fact]
    public async Task Customers_only_see_and_touch_their_own_addresses()
    {
        var alis = await CreateAsync(_ali);

        Assert.Empty(await _service.ListAsync(_sara));
        Assert.Equal(AddressErrors.NotFound, (await _service.UpdateAsync(_sara, alis.Id, Input("Hacked"))).Error!.Code);
        Assert.Equal(AddressErrors.NotFound, (await _service.SetDefaultAsync(_sara, alis.Id)).Error!.Code);
        Assert.Equal(AddressErrors.NotFound, (await _service.DeleteAsync(_sara, alis.Id)).Error!.Code);
        Assert.Equal("Tehran", (await _service.ListAsync(_ali)).Single().City);
    }

    [Fact]
    public async Task Updating_changes_the_fields_but_keeps_one_default()
    {
        var address = await CreateAsync(_ali);

        var updated = await _service.UpdateAsync(_ali, address.Id, Input("Isfahan") with { IsDefault = false });

        Assert.Equal("Isfahan", updated.Value!.City);
        Assert.True(updated.Value.IsDefault);
    }

    [Fact]
    public async Task Deleting_removes_the_row_for_real_and_promotes_the_newest_remaining_address()
    {
        var first = await CreateAsync(_ali, "Tehran");
        _database.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await CreateAsync(_ali, "Shiraz");

        Assert.True((await _service.DeleteAsync(_ali, first.Id)).IsSuccess);

        await using var check = _database.CreateContext();
        Assert.False(await check.CustomerAddresses.IgnoreQueryFilters().AnyAsync(a => a.Id == first.Id));
        Assert.True((await _service.ListAsync(_ali)).Single(a => a.Id == second.Id).IsDefault);
    }

    [Fact]
    public async Task Set_default_moves_the_flag()
    {
        await CreateAsync(_ali, "Tehran");
        var second = await CreateAsync(_ali, "Shiraz");

        Assert.True((await _service.SetDefaultAsync(_ali, second.Id)).IsSuccess);

        Assert.Equal(second.Id, (await _service.ListAsync(_ali)).Single(a => a.IsDefault).Id);
    }

    [Fact]
    public async Task The_address_book_has_a_limit()
    {
        for (var i = 0; i < CustomerAddress.MaxPerCustomer; i++)
            await CreateAsync(_ali, $"City {i}");

        var result = await _service.CreateAsync(_ali, Input());

        Assert.Equal(AddressErrors.LimitReached, result.Error!.Code);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
    }

    [Theory]
    [InlineData("DE", "+4915112345678", null, AddressErrors.CountryNotSupported)]
    [InlineData("IR", "12", null, AccountErrors.PhoneInvalid)]
    [InlineData("IR", "09121234567", "1234", AddressErrors.PostalCodeInvalid)]
    public async Task Business_rules_reject_bad_addresses(string country, string phone, string? postalCode, string expected)
    {
        var result = await _service.CreateAsync(_ali, Input(country: country, phone: phone, postalCode: postalCode));

        Assert.Equal(expected, result.Error!.Code);
        Assert.Empty(await _service.ListAsync(_ali));
    }

    [Fact]
    public async Task A_local_number_is_read_in_the_address_country()
    {
        var result = await _service.CreateAsync(_ali, Input(country: "am", phone: "077 123456", postalCode: "0010"));

        Assert.Equal("+37477123456", result.Value!.PhoneNumber);
        Assert.Equal("AM", result.Value.CountryCode);
    }
}
