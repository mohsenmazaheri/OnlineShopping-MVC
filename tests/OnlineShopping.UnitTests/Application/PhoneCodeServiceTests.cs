using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OnlineShopping.Application.Accounts;
using OnlineShopping.Application.Common;
using OnlineShopping.Domain.Accounts;
using OnlineShopping.Infrastructure.Persistence;
using OnlineShopping.UnitTests.TestSupport;

namespace OnlineShopping.UnitTests.Application;

public sealed class PhoneCodeServiceTests : IDisposable
{
    private const string Phone = "+989121234567";

    private readonly SqliteTestDatabase _database = new();
    private readonly CapturingSmsSender _sms = new();
    private readonly AppDbContext _db;
    private readonly PhoneCodeService _service;

    public PhoneCodeServiceTests()
    {
        _db = _database.CreateContext();
        _service = new PhoneCodeService(_db, _sms, new FixedSmsCapability(true), Options.Create(new PhoneCodeOptions()), _database.Clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private Task<Result<PhoneCodeSent>> SendAsync(PhoneCodePurpose purpose = PhoneCodePurpose.SignIn, string? userId = null) =>
        _service.SendAsync(Phone, purpose, userId, "fa");

    private Task<Result<bool>> VerifyAsync(string code, PhoneCodePurpose purpose = PhoneCodePurpose.SignIn, string? userId = null) =>
        _service.VerifyAsync(Phone, purpose, userId, code);

    private string Wrong() => _sms.LastCodeFor(Phone) == "000000" ? "111111" : "000000";

    [Fact]
    public async Task Sends_a_six_digit_code_and_stores_only_a_salted_hash()
    {
        var result = await SendAsync();

        Assert.Equal(new PhoneCodeSent(180, 60), result.Value);
        var code = _sms.LastCodeFor(Phone);
        Assert.Matches("^[0-9]{6}$", code);

        var stored = await _db.PhoneVerificationCodes.SingleAsync();
        Assert.DoesNotContain(code, stored.CodeHash, StringComparison.Ordinal);
        Assert.NotEmpty(stored.Salt);
    }

    [Fact]
    public async Task The_right_code_works_exactly_once()
    {
        await SendAsync();
        var code = _sms.LastCodeFor(Phone);

        Assert.True((await VerifyAsync(code)).IsSuccess);
        Assert.Equal(AccountErrors.CodeInvalid, (await VerifyAsync(code)).Error!.Code);
    }

    [Fact]
    public async Task Codes_typed_with_Persian_digits_are_accepted()
    {
        await SendAsync();
        var persian = new string(_sms.LastCodeFor(Phone).Select(c => (char)('۰' + (c - '0'))).ToArray());

        Assert.True((await VerifyAsync(persian)).IsSuccess);
    }

    [Fact]
    public async Task Codes_expire()
    {
        await SendAsync();
        _database.Clock.Advance(TimeSpan.FromSeconds(181));

        Assert.Equal(AccountErrors.CodeExpired, (await VerifyAsync(_sms.LastCodeFor(Phone))).Error!.Code);
    }

    [Fact]
    public async Task Five_wrong_guesses_burn_the_code_even_for_the_right_one_afterwards()
    {
        await SendAsync();
        var right = _sms.LastCodeFor(Phone);

        for (var i = 0; i < 4; i++)
            Assert.Equal(AccountErrors.CodeInvalid, (await VerifyAsync(Wrong())).Error!.Code);
        Assert.Equal(AccountErrors.CodeAttemptsExceeded, (await VerifyAsync(Wrong())).Error!.Code);

        Assert.Equal(AccountErrors.CodeInvalid, (await VerifyAsync(right)).Error!.Code);
    }

    [Fact]
    public async Task A_new_code_needs_the_cooldown_and_then_replaces_the_old_one()
    {
        await SendAsync();
        var first = _sms.LastCodeFor(Phone);

        var tooSoon = await SendAsync();
        Assert.Equal(ErrorKind.TooManyRequests, tooSoon.Error!.Kind);
        Assert.Equal(AccountErrors.CodeCooldown, tooSoon.Error.Code);

        _database.Clock.Advance(TimeSpan.FromSeconds(61));
        Assert.True((await SendAsync()).IsSuccess);
        var second = _sms.LastCodeFor(Phone);

        if (first != second)
            Assert.Equal(AccountErrors.CodeInvalid, (await VerifyAsync(first)).Error!.Code);
        Assert.True((await VerifyAsync(second)).IsSuccess);
    }

    [Fact]
    public async Task At_most_five_codes_per_number_per_hour()
    {
        for (var i = 0; i < 5; i++)
        {
            Assert.True((await SendAsync()).IsSuccess);
            _database.Clock.Advance(TimeSpan.FromSeconds(61));
        }

        Assert.Equal(AccountErrors.CodeLimitReached, (await SendAsync()).Error!.Code);

        _database.Clock.Advance(TimeSpan.FromHours(1));
        Assert.True((await SendAsync()).IsSuccess);
    }

    [Fact]
    public async Task Verification_codes_belong_to_the_user_and_purpose_they_were_sent_for()
    {
        await SendAsync(PhoneCodePurpose.VerifyPhone, userId: "u1");
        var code = _sms.LastCodeFor(Phone);

        Assert.Equal(AccountErrors.CodeInvalid, (await VerifyAsync(code, PhoneCodePurpose.VerifyPhone, "u2")).Error!.Code);
        Assert.Equal(AccountErrors.CodeInvalid, (await VerifyAsync(code, PhoneCodePurpose.SignIn)).Error!.Code);
        Assert.True((await VerifyAsync(code, PhoneCodePurpose.VerifyPhone, "u1")).IsSuccess);
    }

    [Fact]
    public async Task A_provider_failure_is_reported_as_could_not_send()
    {
        _sms.Fail = true;

        Assert.Equal(AccountErrors.SmsFailed, (await SendAsync()).Error!.Code);
    }

    [Fact]
    public async Task Nothing_is_sent_when_SMS_is_off()
    {
        var off = new PhoneCodeService(_db, _sms, new FixedSmsCapability(false), Options.Create(new PhoneCodeOptions()), _database.Clock);

        Assert.Equal(AccountErrors.SmsDisabled, (await off.SendAsync(Phone, PhoneCodePurpose.SignIn, null, "fa")).Error!.Code);
        Assert.Empty(_sms.Sent);
    }

    [Fact]
    public async Task Rows_older_than_a_day_are_cleaned_up()
    {
        await SendAsync();
        _database.Clock.Advance(TimeSpan.FromDays(2));
        await SendAsync();

        Assert.Equal(1, await _db.PhoneVerificationCodes.CountAsync());
    }

    [Fact]
    public void Codes_are_random_and_zero_padded()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => PhoneCodeService.NewCode(6)).ToList();

        Assert.All(codes, c => Assert.Matches("^[0-9]{6}$", c));
        Assert.True(codes.Distinct().Count() > 190);
        Assert.Equal(PhoneCodeService.Hash("123456", "s"), PhoneCodeService.Hash("123456", "s"));
        Assert.NotEqual(PhoneCodeService.Hash("123456", "s1"), PhoneCodeService.Hash("123456", "s2"));
    }
}
