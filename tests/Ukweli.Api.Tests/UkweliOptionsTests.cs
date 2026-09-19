using Microsoft.Extensions.Configuration;

namespace Ukweli.Api.Tests;

public class UkweliOptionsTests
{
    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void ReadsTheValuesFromEnvExample()
    {
        var options = UkweliOptions.FromConfiguration(Configuration(
            ("DATABASE_URL", "postgres://ukweli:ukweli@localhost:5432/ukweli"),
            ("APP_URL", "https://ukweli.example"),
            ("AUTH_ENABLED", "true"),
            ("RATE_LIMIT_ANALYZE_PER_MIN", "25"),
            ("SMTP_HOST", "localhost"),
            ("SMTP_PORT", "1025")));

        Assert.Equal("https://ukweli.example", options.AppUrl);
        Assert.True(options.AuthEnabled);
        Assert.Equal(25, options.RateLimitAnalyzePerMinute);
        Assert.Equal("localhost", options.SmtpHost);
        Assert.Equal(1025, options.SmtpPort);
    }

    [Fact]
    public void FallsBackToTheDocumentedDefaults()
    {
        var options = UkweliOptions.FromConfiguration(Configuration(
            ("DATABASE_URL", "postgres://ukweli:ukweli@localhost:5432/ukweli")));

        Assert.Equal(UkweliOptions.DefaultAppUrl, options.AppUrl);
        Assert.Equal(UkweliOptions.DefaultRateLimitAnalyzePerMinute, options.RateLimitAnalyzePerMinute);
        // Auth off by default: the demo has to work fully without it.
        Assert.False(options.AuthEnabled);
    }

    [Fact]
    public void FailsLoudlyWhenDatabaseUrlIsMissing()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => UkweliOptions.FromConfiguration(Configuration()));

        Assert.Contains("DATABASE_URL", exception.Message, StringComparison.Ordinal);
        Assert.Contains(".env", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TreatsABlankKeyAsNotConfigured()
    {
        // .env spells "unset" as an empty value, not an absent line.
        var options = UkweliOptions.FromConfiguration(Configuration(
            ("DATABASE_URL", "postgres://ukweli:ukweli@localhost:5432/ukweli"),
            ("ANTHROPIC_API_KEY", "   ")));

        Assert.False(options.ModelConfigured);
        Assert.Null(options.AnthropicApiKey);
    }

    [Fact]
    public void ReportsTheModelAsConfiguredWhenAKeyIsPresent()
    {
        var options = UkweliOptions.FromConfiguration(Configuration(
            ("DATABASE_URL", "postgres://ukweli:ukweli@localhost:5432/ukweli"),
            ("ANTHROPIC_API_KEY", "sk-ant-not-a-real-key")));

        Assert.True(options.ModelConfigured);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    public void ParsesTheBooleanSpellingsAEnvFileUses(string value, bool expected)
    {
        Assert.Equal(expected, UkweliOptions.ParseBool(value, defaultValue: !expected));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("maybe")]
    public void FallsBackToTheDefaultForAnUnrecognisedBoolean(string? value)
    {
        Assert.True(UkweliOptions.ParseBool(value, defaultValue: true));
        Assert.False(UkweliOptions.ParseBool(value, defaultValue: false));
    }

    [Fact]
    public void IgnoresANonNumericRateLimit()
    {
        var options = UkweliOptions.FromConfiguration(Configuration(
            ("DATABASE_URL", "postgres://ukweli:ukweli@localhost:5432/ukweli"),
            ("RATE_LIMIT_ANALYZE_PER_MIN", "lots")));

        Assert.Equal(UkweliOptions.DefaultRateLimitAnalyzePerMinute, options.RateLimitAnalyzePerMinute);
    }
}
