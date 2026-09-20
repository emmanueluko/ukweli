namespace Ukweli.Api.Auth;

/// <summary>
/// The one place the session cookie's options are defined.
/// </summary>
/// <remarks>
/// Defined once because they have to match everywhere the cookie is written.
/// A renewal that set different options would replace the cookie rather than
/// extend it, and a mismatched Path is the classic way to end up with two
/// cookies of the same name and an unpredictable session.
/// </remarks>
public static class SessionCookie
{
    public static CookieOptions Options(HttpContext context, DateTimeOffset expiresAt) => new()
    {
        // Not readable from JavaScript, so a script injected into the page
        // cannot lift the session.
        HttpOnly = true,
        // Secure only where the request arrived over HTTPS: in development the
        // app is served over plain HTTP and a Secure cookie would be dropped.
        Secure = context.Request.IsHttps,
        // Lax, not Strict: a sign-in link arrives from an email client, and a
        // Strict cookie would not be sent on that first navigation.
        SameSite = SameSiteMode.Lax,
        Path = "/",
        // An explicit expiry rather than a session cookie, so closing the
        // browser does not sign the person out.
        Expires = expiresAt,
        IsEssential = true,
    };
}
