using Ukweli.Contracts;
using Ukweli.Data;

namespace Ukweli.Api;

public static class SourceEndpoints
{
    public static void MapSourceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/sources/{id}", async (
            string id,
            SourceRepository sources,
            CancellationToken cancellationToken) =>
        {
            var source = await sources.FindAsync(id, cancellationToken);

            return source is null
                ? ApplicationSetup.Problem(
                    StatusCodes.Status404NotFound,
                    ErrorCodes.NotFound,
                    $"No source with id '{id}'.",
                    retryable: false)
                : Results.Ok(SourceRepository.ToResponse(source));
        })
        .WithName("GetSource")
        .WithSummary("Fetch one curated source")
        .WithDescription(
            "Returns the source's metadata, the verbatim excerpt Ukweli holds, and the URL "
            + "it came from. externalContentMayChange is always true: the excerpt is what the "
            + "document said when a human checked it, not whatever that address serves today.")
        .Produces<SourceResponse>()
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/api/examples", (CancellationToken _) =>
            // Populated in Phase 2, where the seeded claims are defined. An
            // empty list is the honest answer until then, and lets the web
            // placeholder call this endpoint today.
            Results.Ok(Array.Empty<ExampleClaimResponse>()))
        .WithName("ListExamples")
        .WithSummary("List the example claims offered on the home screen")
        .Produces<IReadOnlyList<ExampleClaimResponse>>();
    }
}
