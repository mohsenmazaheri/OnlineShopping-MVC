using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OnlineShopping.Api.Endpoints;
using OnlineShopping.Application.Accounts;
using OnlineShopping.Application.Customers;
using OnlineShopping.IntegrationTests.TestSupport;

namespace OnlineShopping.IntegrationTests;

/// <summary>Profile, address book, personal data download and account deletion against PostgreSQL.</summary>
public sealed class ProfileTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse 42";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static AddressInput Address(string city = "Tehran", bool isDefault = false) =>
        new("Home", "Ali Rezaei", "09121234567", "IR", "Tehran", city, "Valiasr St, No. 10", null, "1234567890", isDefault);

    private async Task<(HttpClient Client, string Email)> RegisterAsync()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/api/v1/account/register", new RegisterRequest(email, Password, null, "fa"))).EnsureSuccessStatusCode();
        return (client, email);
    }

    [Fact]
    public async Task Store_info_includes_the_checkout_settings()
    {
        using var client = factory.CreateClient();

        var store = await client.GetFromJsonAsync<StoreInfoResponse>("/api/v1/store", Json);

        Assert.True(store!.Checkout.Guest);
        Assert.Equal(["IR"], store.Checkout.ShippingCountries);
    }

    [Fact]
    public async Task The_profile_can_be_edited()
    {
        var (client, _) = await RegisterAsync();
        using (client)
        {
            using var response = await client.PutAsJsonAsync("/api/v1/account/profile", new UpdateProfileRequest("Ali", "en"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var me = await client.GetFromJsonAsync<CurrentUser>("/api/v1/account/me", Json);
            Assert.Equal(("Ali", "en", true), (me!.DisplayName, me.PreferredLocale, me.HasPassword));

            using var bad = await client.PutAsJsonAsync("/api/v1/account/profile", new UpdateProfileRequest("Ali", "de"));
            await AssertProblemAsync(bad, HttpStatusCode.BadRequest, ProfileErrors.LocaleNotSupported);
        }
    }

    [Fact]
    public async Task Changing_the_password_keeps_this_session_and_replaces_the_old_password()
    {
        var (client, email) = await RegisterAsync();
        using (client)
        {
            using var wrong = await client.PostAsJsonAsync("/api/v1/account/change-password", new ChangePasswordRequest("nope", "brand new pass 7"));
            await AssertProblemAsync(wrong, HttpStatusCode.BadRequest, ProfileErrors.PasswordIncorrect);

            using var ok = await client.PostAsJsonAsync("/api/v1/account/change-password", new ChangePasswordRequest(Password, "brand new pass 7"));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/me")).StatusCode);
        }

        using var other = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, false))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, "brand new pass 7", false))).StatusCode);
    }

    [Fact]
    public async Task The_address_book_supports_add_edit_default_and_delete()
    {
        var (client, _) = await RegisterAsync();
        using (client)
        {
            using var created = await client.PostAsJsonAsync("/api/v1/account/addresses", Address("Tehran"));
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            var first = (await created.Content.ReadFromJsonAsync<AddressView>(Json))!;
            Assert.True(first.IsDefault);
            Assert.Equal("+989121234567", first.PhoneNumber);

            var second = (await (await client.PostAsJsonAsync("/api/v1/account/addresses", Address("Shiraz"))).Content.ReadFromJsonAsync<AddressView>(Json))!;
            (await client.PostAsync($"/api/v1/account/addresses/{second.Id}/default", null)).EnsureSuccessStatusCode();
            (await client.PutAsJsonAsync($"/api/v1/account/addresses/{first.Id}", Address("Isfahan"))).EnsureSuccessStatusCode();

            var list = await client.GetFromJsonAsync<List<AddressView>>("/api/v1/account/addresses", Json);
            Assert.Equal([second.Id, first.Id], list!.Select(a => a.Id));
            Assert.Equal("Isfahan", list![1].City);

            (await client.DeleteAsync($"/api/v1/account/addresses/{second.Id}")).EnsureSuccessStatusCode();
            Assert.True(Assert.Single((await client.GetFromJsonAsync<List<AddressView>>("/api/v1/account/addresses", Json))!).IsDefault);
        }
    }

    [Fact]
    public async Task Another_customers_address_answers_404()
    {
        var (owner, _) = await RegisterAsync();
        var (intruder, _) = await RegisterAsync();
        using (owner)
        using (intruder)
        {
            var address = (await (await owner.PostAsJsonAsync("/api/v1/account/addresses", Address())).Content.ReadFromJsonAsync<AddressView>(Json))!;

            await AssertProblemAsync(await intruder.PutAsJsonAsync($"/api/v1/account/addresses/{address.Id}", Address("Hacked")),
                HttpStatusCode.NotFound, AddressErrors.NotFound);
            Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync($"/api/v1/account/addresses/{address.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsync($"/api/v1/account/addresses/{address.Id}/default", null)).StatusCode);
            Assert.Empty((await intruder.GetFromJsonAsync<List<AddressView>>("/api/v1/account/addresses", Json))!);
            Assert.Equal("Tehran", Assert.Single((await owner.GetFromJsonAsync<List<AddressView>>("/api/v1/account/addresses", Json))!).City);
        }
    }

    [Fact]
    public async Task Invalid_addresses_are_rejected_with_codes()
    {
        var (client, _) = await RegisterAsync();
        using (client)
        {
            using var empty = await client.PostAsJsonAsync("/api/v1/account/addresses", Address() with { City = "", RecipientName = "" });
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            var errors = JsonElement.Parse(await empty.Content.ReadAsStringAsync()).GetProperty("errors");
            Assert.Equal(AddressErrors.CityRequired, errors.GetProperty("city")[0].GetString());

            await AssertProblemAsync(await client.PostAsJsonAsync("/api/v1/account/addresses", Address() with { CountryCode = "DE" }),
                HttpStatusCode.BadRequest, AddressErrors.CountryNotSupported);
        }
    }

    [Fact]
    public async Task Account_pages_need_a_session()
    {
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/addresses")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/personal-data")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/account/delete", new DeleteAccountRequest(null))).StatusCode);
    }

    [Fact]
    public async Task Personal_data_downloads_as_a_json_file()
    {
        var (client, email) = await RegisterAsync();
        using (client)
        {
            (await client.PostAsJsonAsync("/api/v1/account/addresses", Address())).EnsureSuccessStatusCode();

            using var response = await client.GetAsync("/api/v1/account/personal-data");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
            Assert.Equal("personal-data.json", response.Content.Headers.ContentDisposition!.FileName?.Trim('"'));
            Assert.True(response.Headers.CacheControl!.NoStore);
            var export = (await response.Content.ReadFromJsonAsync<PersonalDataExport>(Json))!;
            Assert.Equal(email, export.Account.Email);
            Assert.Equal("Tehran", Assert.Single(export.Addresses).City);
        }
    }

    [Fact]
    public async Task Deleting_the_account_erases_it_and_frees_the_email()
    {
        var (client, email) = await RegisterAsync();
        using (client)
        {
            (await client.PostAsJsonAsync("/api/v1/account/addresses", Address())).EnsureSuccessStatusCode();

            await AssertProblemAsync(await client.PostAsJsonAsync("/api/v1/account/delete", new DeleteAccountRequest("wrong")),
                HttpStatusCode.BadRequest, ProfileErrors.PasswordIncorrect);

            using var deleted = await client.PostAsJsonAsync("/api/v1/account/delete", new DeleteAccountRequest(Password));
            Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/me")).StatusCode);
        }

        using var other = factory.CreateClient();
        await AssertProblemAsync(await other.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password, false)),
            HttpStatusCode.BadRequest, AccountErrors.InvalidCredentials);
        using var again = await other.PostAsJsonAsync("/api/v1/account/register", new RegisterRequest(email, Password, null, "fa"));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<AddressView>>("/api/v1/account/addresses", Json))!);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        }
    }
}
