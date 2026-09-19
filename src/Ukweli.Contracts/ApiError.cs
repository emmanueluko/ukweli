namespace Ukweli.Contracts;

/// <summary>
/// The single error envelope every non-2xx response uses:
/// <c>{ "error": { "code", "message", "retryable" } }</c>.
/// </summary>
/// <param name="Error">The error detail.</param>
public sealed record ApiErrorResponse(ApiErrorDetail Error)
{
    public static ApiErrorResponse Create(string code, string message, bool retryable) =>
        new(new ApiErrorDetail(code, message, retryable));
}

/// <param name="Code">A stable machine-readable code from <see cref="ErrorCodes"/>.</param>
/// <param name="Message">A human-readable explanation, safe to show a user.</param>
/// <param name="Retryable">
/// Whether repeating the same request could plausibly succeed. True for
/// timeouts and upstream failures; false for validation errors and 404s.
/// </param>
public sealed record ApiErrorDetail(string Code, string Message, bool Retryable);

public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string NotFound = "not_found";
    public const string Unauthorized = "unauthorized";
    public const string RateLimited = "rate_limited";
    public const string AiUnavailable = "ai_unavailable";
    public const string Internal = "internal_error";
}
