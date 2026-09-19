using Microsoft.EntityFrameworkCore;

namespace Ukweli.Data;

/// <summary>
/// The application's EF Core context.
/// </summary>
/// <remarks>
/// Phase 0 defines no entities — it exists so <c>/healthz</c> can make a real
/// round trip to Postgres. The <c>sources</c> table arrives in Phase 1 and
/// <c>claim_analyses</c> in Phase 2.
/// </remarks>
public class UkweliDbContext(DbContextOptions<UkweliDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        SnakeCaseNaming.Apply(modelBuilder);
    }

    /// <summary>
    /// A real round trip to Postgres, used by the health endpoint. Returns
    /// false rather than throwing, so a health check can report a degraded
    /// database without the endpoint itself failing.
    /// </summary>
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }
}
