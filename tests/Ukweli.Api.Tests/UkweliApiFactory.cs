using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ukweli.Api.Tests;

/// <summary>
/// Boots the real application for HTTP-level tests, pointed at the test
/// database rather than the one being demoed.
/// </summary>
/// <remarks>
/// These settings outrank the developer's <c>.env</c>, which the application
/// registers as its lowest-priority configuration source. Without that
/// ordering a real key sitting in <c>.env</c> would silently decide what these
/// tests exercise.
/// </remarks>
public sealed class UkweliApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultTestDatabaseUrl =
        "postgres://ukweli:ukweli@localhost:5432/ukweli_test";

    private readonly string _databaseUrl;
    private readonly string _anthropicApiKey;
    private readonly bool _authEnabled;
    private readonly int? _smtpPort;

    public UkweliApiFactory(
        string? databaseUrl = null,
        string? anthropicApiKey = null,
        bool authEnabled = false,
        int? smtpPort = null)
    {
        _authEnabled = authEnabled;
        _smtpPort = smtpPort;
        // TEST_DATABASE_URL lets CI point at its own service container.
        _databaseUrl = databaseUrl
            ?? Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
            ?? DefaultTestDatabaseUrl;
        _anthropicApiKey = anthropicApiKey ?? string.Empty;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("DATABASE_URL", _databaseUrl);
        builder.UseSetting("ANTHROPIC_API_KEY", _anthropicApiKey);
        builder.UseSetting("AUTH_ENABLED", _authEnabled ? "true" : "false");
        // PORT is left blank: WebApplicationFactory supplies its own server.
        builder.UseSetting("PORT", string.Empty);

        if (_smtpPort is { } port)
        {
            builder.UseSetting("SMTP_PORT", port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
