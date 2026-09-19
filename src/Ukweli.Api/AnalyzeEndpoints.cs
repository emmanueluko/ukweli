using System.Diagnostics;
using Ukweli.Contracts;
using Ukweli.Data;

namespace Ukweli.Api;

public static class AnalyzeEndpoints
{
    public static void MapAnalyzeEndpoints(this WebApplication app)
    {
        app.MapPost("/api/analyze", async (
            AnalyzeRequest request,
            AnalyzeService analyze,
            AnalysisAssembler assembler,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("Ukweli.Analyze");
            var started = Stopwatch.GetTimestamp();

            var validation = AnalyzeService.Validate(request);
            if (!validation.IsValid)
            {
                return ApplicationSetup.Problem(
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed,
                    validation.Message!,
                    retryable: false);
            }

            var seed = analyze.MatchSeed(validation.Text, validation.ExampleId);
            if (seed is null)
            {
                // Free text goes to the model in Phase 3. Until that exists,
                // saying so plainly beats inventing a verdict.
                return ApplicationSetup.Problem(
                    StatusCodes.Status503ServiceUnavailable,
                    ErrorCodes.AiUnavailable,
                    "Checking a new claim is not available yet. Try one of the examples from "
                    + "/api/examples.",
                    retryable: true);
            }

            var analysis = await analyze.ResolveSeededAsync(seed, userId: null, cancellationToken);
            var response = await assembler.AssembleAsync(analysis, cancellationToken);

            var elapsedMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            // Ids, verdicts and timings only. Claim text is never logged.
            logger.SeededAnalysis(seed.Id, analysis.Status, analysis.Id, elapsedMs);

            return Results.Ok(response);
        })
        .WithName("Analyze")
        .WithSummary("Check a civic claim")
        .WithDescription(
            "Returns one of four verdicts with the sources behind it. A claim the curated "
            + "store cannot speak to returns insufficient_evidence rather than a guess, and no "
            + "response carries a confidence score. Only the normalised claim is stored; the "
            + "raw text is never persisted or logged.")
        .Produces<AnalysisResponse>()
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/results/{id}", async (
            string id,
            AnalysisRepository analyses,
            AnalysisAssembler assembler,
            CancellationToken cancellationToken) =>
        {
            var analysis = await analyses.FindAsync(id, cancellationToken);

            return analysis is null
                ? ApplicationSetup.Problem(
                    StatusCodes.Status404NotFound,
                    ErrorCodes.NotFound,
                    $"No result with id '{id}'.",
                    retryable: false)
                : Results.Ok(await assembler.AssembleAsync(analysis, cancellationToken));
        })
        .WithName("GetResult")
        .WithSummary("Reload a stored result by id")
        .WithDescription("The id behind a /r/{id} share link.")
        .Produces<AnalysisResponse>()
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
