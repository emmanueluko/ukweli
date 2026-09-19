namespace Ukweli.Api.Auth;

/// <summary>
/// Auth logging, with no email address in any message.
/// </summary>
/// <remarks>
/// Who asked to sign in is not something the logs need to know, and an address
/// in a log outlives the request and travels wherever the logs travel.
/// </remarks>
internal static partial class AuthLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Magic link sent via {Transport}")]
    public static partial void MagicLinkSent(this ILogger logger, string transport);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Magic link delivery failed with status {StatusCode}")]
    public static partial void MagicLinkFailed(this ILogger logger, int statusCode);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Magic link rejected: {Reason}")]
    public static partial void MagicLinkRejected(this ILogger logger, string reason);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Rate limit reached for an address on {Route}")]
    public static partial void RateLimited(this ILogger logger, string route);
}
