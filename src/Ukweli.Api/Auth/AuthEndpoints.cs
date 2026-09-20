using Ukweli.Contracts;
using Ukweli.Data;

namespace Ukweli.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<UkweliOptions>();

        // AUTH_ENABLED=false hides every auth route. Nothing else changes.
        if (!options.AuthEnabled)
        {
            return;
        }

        app.MapPost("/api/auth/magic-link", async (
            MagicLinkRequest request,
            MagicLinkService magicLinks,
            RateLimiter rateLimiter,
            HttpContext context,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            if (!MagicLinkService.LooksLikeEmail(request?.Email))
            {
                return ApplicationSetup.Problem(
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.ValidationFailed,
                    "Enter an email address so the sign-in link has somewhere to go.",
                    retryable: false);
            }

            if (!rateLimiter.TryAcquire($"magic-link:{RateLimiter.ClientKey(context)}"))
            {
                loggerFactory.CreateLogger("Ukweli.Auth").RateLimited("/api/auth/magic-link");

                return ApplicationSetup.Problem(
                    StatusCodes.Status429TooManyRequests,
                    ErrorCodes.RateLimited,
                    "Too many sign-in requests. Wait a minute and try again.",
                    retryable: true);
            }

            await magicLinks.RequestAsync(request!.Email!, cancellationToken);

            // Always 202, whether or not the address is known. Answering
            // differently would turn this endpoint into a way to discover who
            // has an account.
            return Results.Accepted(value: new MagicLinkAccepted(
                "If that address can receive mail, a sign-in link is on its way."));
        })
        .WithName("RequestMagicLink")
        .WithSummary("Request a sign-in link")
        .WithDescription(
            "Always returns 202, whether or not the address is known, so the endpoint cannot "
            + "be used to discover who has an account.")
        .Produces<MagicLinkAccepted>(StatusCodes.Status202Accepted)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status429TooManyRequests);

        app.MapGet("/api/auth/verify", async (
            string? token,
            MagicLinkService magicLinks,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var sessionToken = await magicLinks.VerifyAsync(token, cancellationToken);
            var appUrl = options.AppUrl.TrimEnd('/');

            if (sessionToken is null)
            {
                // A person followed this from their email client, so they are
                // sent back to the sign-in screen with an explanation — not
                // handed a JSON error envelope to interpret.
                return Results.Redirect($"{appUrl}/sign-in?error=link_expired");
            }

            context.Response.Cookies.Append(
                MagicLinkService.CookieName,
                sessionToken,
                SessionCookie.Options(
                    context, DateTimeOffset.UtcNow.Add(MagicLinkService.SessionIdleLifetime)));

            // This endpoint is opened by a human clicking a link in an email,
            // not by a script, so it answers with a page rather than with JSON.
            // Returning {"authenticated":true} left people staring at raw JSON
            // wondering whether it had worked.
            return Results.Redirect($"{appUrl}/my-checks");
        })
        .WithName("VerifyMagicLink")
        .WithSummary("Exchange a sign-in link for a session")
        .WithDescription(
            "Opened from an email. Sets the session cookie and redirects into the app; "
            + "an expired or reused link redirects to the sign-in screen.")
        .Produces(StatusCodes.Status302Found);

        app.MapPost("/api/auth/sign-out", async (
            MagicLinkService magicLinks,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            context.Request.Cookies.TryGetValue(MagicLinkService.CookieName, out var token);
            await magicLinks.SignOutAsync(token, cancellationToken);
            context.Response.Cookies.Delete(MagicLinkService.CookieName);

            return Results.Ok(new SignedIn(false));
        })
        .WithName("SignOut")
        .WithSummary("End the current session")
        .Produces<SignedIn>();

        app.MapGet("/api/me", async (
            CurrentUser currentUser,
            MagicLinkService magicLinks,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.IsAuthenticated)
            {
                return Unauthorized();
            }

            var user = await magicLinks.FindUserAsync(currentUser.UserId!, cancellationToken);

            return user is null
                ? Unauthorized()
                : Results.Ok(new MeResponse(user.Email, user.CreatedAt));
        })
        .WithName("Me")
        .WithSummary("The signed-in account")
        .Produces<MeResponse>()
        .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/me/analyses", async (
            CurrentUser currentUser,
            AnalysisRepository analyses,
            AnalysisAssembler assembler,
            CancellationToken cancellationToken) =>
        {
            if (!currentUser.IsAuthenticated)
            {
                return Unauthorized();
            }

            var rows = await analyses.ListForUserAsync(
                currentUser.UserId!, cancellationToken: cancellationToken);

            var results = new List<AnalysisResponse>(rows.Count);
            foreach (var row in rows)
            {
                results.Add(await assembler.AssembleAsync(row, cancellationToken));
            }

            return Results.Ok(results);
        })
        .WithName("MyAnalyses")
        .WithSummary("Claims this account has checked, newest first")
        .Produces<IReadOnlyList<AnalysisResponse>>()
        .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized);
    }

    private static IResult Unauthorized() =>
        ApplicationSetup.Problem(
            StatusCodes.Status401Unauthorized,
            ErrorCodes.Unauthorized,
            "Sign in to see your saved checks.",
            retryable: false);
}

/// <param name="Email">Where to send the sign-in link.</param>
public sealed record MagicLinkRequest(string? Email);

public sealed record MagicLinkAccepted(string Message);

public sealed record SignedIn(bool Authenticated);

/// <param name="Email">The signed-in address.</param>
/// <param name="CreatedAt">When the account first signed in.</param>
public sealed record MeResponse(string Email, DateTimeOffset CreatedAt);
