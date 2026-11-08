using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using OnlineShopping_MVC.Data;
using OnlineShopping_MVC.Data.Interceptors;

namespace OnlineShopping_MVC.Tests.TestSupport;

/// <summary>
/// A relational in-memory SQLite database built from the real <see cref="AppDbContext"/> model,
/// so query filters, unique indexes, FKs and check constraints are exercised (unlike the EF InMemory provider).
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDatabase(DateTimeOffset? now = null)
    {
        Clock = new FakeTimeProvider(now ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public FakeTimeProvider Clock { get; }

    /// <summary>The open connection; the in-memory database lives as long as it stays open.</summary>
    public SqliteConnection Connection => _connection;

    public DbContextOptions<AppDbContext> Options => new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(_connection)
        .AddInterceptors(new AuditableEntityInterceptor(Clock))
        .Options;

    /// <summary>Each call returns a fresh context (empty change tracker) over the same database.</summary>
    public AppDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}
