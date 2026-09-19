using System.Diagnostics;
using Ukweli.Api.Ai;
using Ukweli.Api.Auth;
using Ukweli.Contracts;
using Ukweli.Data.Entities;
using Ukweli.Evidence;
using Ukweli.Data;

namespace Ukweli.Api;

public static class AnalyzeEndpoints
{
    public static void MapAnalyzeEndpoints(this WebApplication app)
    {
        app.MapPost("/api/analyze", async (
            AnalyzeRequest request,
            AnalyzeService analyze,
            AnalysisPipeline pipeline,
            AnalysisAssembler assembler,
            RateLimiter rateLimiter,
            CurrentUser currentUser,
            HttpContext context,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("Ukweli.Analyze");
            var started = Stopwatch.GetTimestamp();

            // Per IP, signed in or not: every analysis costs a model call, and
            // requiring a session to spend that would only mean signing in first.
            if (!rateLimiter.TryAcquire($"analyze:{RateLimiter.ClientKey(context)}"))
            {
                logger.RateLimited("/api/analyze");

                return ApplicationSetup.Problem(
                    StatusCodes.Status429TooManyRequests,
                    ErrorCodes.RateLimited,
                    "You have checked several claims in the last minute. Wait a moment and "
                    + "try again.",
                    retryable: true);
            }

            var validation = AnalyzeService.Validate(request);
            if (!validation.IsValid)
            {
                return ApplicationSetup.Problem(
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed,
                    validation.Message!,
                    retryable: false);
            }

            // A seeded claim never touches the model.
            var seed = analyze.MatchSeed(validation.Text, validation.ExampleId);

            ClaimAnalysis analysis;
            if (seed is not null)
            {
                analysis = await analyze.ResolveSeededAsync(seed, currentUser.UserId, cancellationToken);
            }
            else
            {
                try
                {
                    analysis = await pipeline.RunAsync(validation.Text, currentUser.UserId, cancellationToken);
                }
                catch (AiUnavailableException ex)
                {
                    // Safety rule 8: a failed model call never produces a result.
                    logger.ModelUnavailable(
                        (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, ex.Message);

                    return ApplicationSetup.Problem(
                        StatusCodes.Status503ServiceUnavailable,
                        ErrorCodes.AiUnavailable,
                        "Ukweli could not check this claim just now. Try again in a moment, or "
                        + "use one of the examples.",
                        retryable: true);
                }
            }

            var response = await assembler.AssembleAsync(analysis, cancellationToken);
            var elapsedMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            // Ids, verdicts and timings only. Claim text is never logged.
            if (seed is not null)
            {
                logger.SeededAnalysis(seed.Id, analysis.Status, analysis.Id, elapsedMs);
            }
            else
            {
                logger.FreeTextAnalysis(analysis.Status, analysis.Id, elapsedMs);
            }

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
        .Produces<ApiErrorResponse>(StatusCodes.Status429TooManyRequests)
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
