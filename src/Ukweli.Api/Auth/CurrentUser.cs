namespace Ukweli.Api.Auth;

/// <summary>
/// Who is making this request, resolved once per request.
/// </summary>
/// <remarks>
/// With <c>AUTH_ENABLED=false</c> this is always anonymous and nothing else in
/// the application behaves differently — the demo has to work fully with auth
/// off.
/// </remarks>
public sealed class CurrentUser
{
    public string? UserId { get; private set; }

    public bool IsAuthenticated => UserId is not null;

    public void SignedInAs(string userId) => UserId = userId;
}

/// <summary>Reads the session cookie and resolves it, when auth is on at all.</summary>
public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        UkweliOptions options,
        CurrentUser currentUser,
        MagicLinkService magicLinks)
    {
        if (options.AuthEnabled &&
            context.Request.Cookies.TryGetValue(MagicLinkService.CookieName, out var token))
        {
            if (await magicLinks.ResolveAsync(token, context.RequestAborted) is { } session)
            {
                currentUser.SignedInAs(session.UserId);

                // The server moved the window, so the browser is told too.
                // Without this the cookie would still carry its original expiry
                // and the browser would discard it while the session was live.
                if (session.RenewedUntil is { } renewedUntil)
                {
                    context.Response.Cookies.Append(
                        MagicLinkService.CookieName,
                        token,
                        SessionCookie.Options(context, renewedUntil));
                }
            }
        }

        await next(context);
    }
}
