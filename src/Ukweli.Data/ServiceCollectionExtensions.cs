using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ukweli.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="UkweliDbContext"/> against the given
    /// <c>DATABASE_URL</c>, in either the documented URL form or a native
    /// Npgsql connection string.
    /// </summary>
    public static IServiceCollection AddUkweliData(this IServiceCollection services, string? databaseUrl)
    {
        var connectionString = DatabaseUrl.ToConnectionString(databaseUrl);

        services.AddDbContext<UkweliDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                // The health check must fail fast and report a degraded database
                // rather than hanging the request while Postgres is down.
                npgsql.CommandTimeout(10);
            }));

        return services;
    }
}
