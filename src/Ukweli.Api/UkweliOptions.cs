namespace Ukweli.Api;

/// <summary>
/// The environment variables from <c>.env.example</c>, read once at startup.
/// </summary>
/// <remarks>
/// Nothing here is ever sent to a client. <see cref="AnthropicApiKey"/> in
/// particular is surfaced only as the boolean <see cref="ModelConfigured"/>.
/// </remarks>
public sealed class UkweliOptions
{
    public const string DefaultAppUrl = "http://localhost:5173";
    public const int DefaultRateLimitAnalyzePerMinute = 10;

    public required string DatabaseUrl { get; init; }
    public string? AnthropicApiKey { get; init; }
    public string AppUrl { get; init; } = DefaultAppUrl;
    public bool AuthEnabled { get; init; }
    public string? AuthSecret { get; init; }
    public string? ResendApiKey { get; init; }
    public string? SmtpHost { get; init; }
    public int? SmtpPort { get; init; }
    public int RateLimitAnalyzePerMinute { get; init; } = DefaultRateLimitAnalyzePerMinute;

    /// <summary>Whether an Anthropic key is present. Never exposes the key itself.</summary>
    public bool ModelConfigured => !string.IsNullOrWhiteSpace(AnthropicApiKey);

    public static UkweliOptions FromConfiguration(IConfiguration configuration) => new()
    {
        DatabaseUrl = configuration["DATABASE_URL"]
            ?? throw new InvalidOperationException(
                "DATABASE_URL is not set. Copy .env.example to .env and fill it in."),
        AnthropicApiKey = Blank(configuration["ANTHROPIC_API_KEY"]),
        AppUrl = Blank(configuration["APP_URL"]) ?? DefaultAppUrl,
        AuthEnabled = ParseBool(configuration["AUTH_ENABLED"], defaultValue: false),
        AuthSecret = Blank(configuration["BETTER_AUTH_SECRET"]),
        ResendApiKey = Blank(configuration["RESEND_API_KEY"]),
        SmtpHost = Blank(configuration["SMTP_HOST"]),
        SmtpPort = ParseInt(configuration["SMTP_PORT"]),
        RateLimitAnalyzePerMinute =
            ParseInt(configuration["RATE_LIMIT_ANALYZE_PER_MIN"]) ?? DefaultRateLimitAnalyzePerMinute,
    };

    /// <summary>Treats an empty or whitespace value as absent, which is how .env files spell "unset".</summary>
    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static bool ParseBool(string? value, bool defaultValue) => Blank(value)?.ToLowerInvariant() switch
    {
        "true" or "1" or "yes" or "on" => true,
        "false" or "0" or "no" or "off" => false,
        _ => defaultValue,
    };

    internal static int? ParseInt(string? value) =>
        int.TryParse(Blank(value), out var parsed) ? parsed : null;
}
