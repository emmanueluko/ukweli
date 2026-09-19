using Microsoft.EntityFrameworkCore;
using Ukweli.Data;

namespace Ukweli.Api.Tests;

/// <summary>
/// Brings the test database up to the current migrations, once for the whole
/// run.
/// </summary>
/// <remarks>
/// Migrations rather than <c>EnsureCreated</c>: the latter builds tables
/// straight from the model and writes no migration history, so the schema
/// silently stops matching what a real deployment applies, and the next
/// migration has nothing to build on. Running the real migrations here means
/// the tests exercise the schema that ships.
/// </remarks>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public string DatabaseUrlValue { get; } =
        Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
        ?? UkweliApiFactory.DefaultTestDatabaseUrl;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<UkweliDbContext>()
            .UseNpgsql(DatabaseUrl.ToConnectionString(DatabaseUrlValue))
            .Options;

        await using var db = new UkweliDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// Tests that touch Postgres share this xUnit collection, so they do not run in
/// parallel against the same database and the schema is prepared once.
/// </summary>
[CollectionDefinition("database")]
public sealed class DatabaseTests : ICollectionFixture<DatabaseFixture>;
