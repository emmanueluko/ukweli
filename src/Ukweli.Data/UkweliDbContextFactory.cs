using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ukweli.Data;

/// <summary>
/// Lets <c>dotnet ef</c> build a context without starting the web application.
/// </summary>
/// <remarks>
/// With this, migrations are generated against this project alone, so
/// <c>Microsoft.EntityFrameworkCore.Design</c> never has to be referenced by
/// <c>Ukweli.Api</c> — a design-time tool has no business in the image that
/// serves production traffic.
/// </remarks>
public sealed class UkweliDbContextFactory : IDesignTimeDbContextFactory<UkweliDbContext>
{
    /// <summary>Used only when DATABASE_URL is unset, to scaffold a migration offline.</summary>
    private const string OfflineFallback =
        "Host=localhost;Port=5432;Database=ukweli;Username=ukweli;Password=ukweli";

    public UkweliDbContext CreateDbContext(string[] args)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        var connectionString = string.IsNullOrWhiteSpace(databaseUrl)
            ? OfflineFallback
            : DatabaseUrl.ToConnectionString(databaseUrl);

        var options = new DbContextOptionsBuilder<UkweliDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new UkweliDbContext(options);
    }
}
