using Ukweli.Contracts;
using Ukweli.Data;

namespace Ukweli.Api;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/healthz", async (
            UkweliDbContext db,
            UkweliOptions options,
            CancellationToken cancellationToken) =>
        {
            var dbReachable = await db.CanConnectAsync(cancellationToken);

            var response = new HealthResponse(
                Ok: dbReachable,
                Db: dbReachable,
                ModelConfigured: options.ModelConfigured);

            // A degraded database is reported as 503 so container orchestrators
            // and `curl --fail` both see the failure, while the body still says
            // exactly which dependency is down.
            return dbReachable
                ? Results.Ok(response)
                : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("Health");
    }
}
