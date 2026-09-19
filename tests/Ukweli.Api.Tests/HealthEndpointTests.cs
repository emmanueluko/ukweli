using System.Net;
using System.Net.Http.Json;
using Ukweli.Contracts;

namespace Ukweli.Api.Tests;

/// <summary>
/// Phase 0's acceptance criterion: <c>/healthz</c> reports <c>db: true</c>
/// against a running Postgres. Requires the dev stack
/// (<c>docker compose -f docker-compose.dev.yml up</c>).
/// </summary>
[Collection("database")]
public class HealthEndpointTests
{
    private const string HealthPath = "/healthz";

    [Fact]
    public async Task ReportsHealthyAgainstARunningDatabase()
    {
        using var factory = new UkweliApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(HealthPath);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.Db, "Expected db: true. Is the dev Postgres running?");
        Assert.True(body.Ok);
    }

    [Fact]
    public async Task ReportsTheModelAsNotConfiguredWhenNoKeyIsSet()
    {
        using var factory = new UkweliApiFactory(anthropicApiKey: string.Empty);
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<HealthResponse>(HealthPath);

        Assert.NotNull(body);
        Assert.False(body.ModelConfigured);
    }

    [Fact]
    public async Task ReportsTheModelAsConfiguredWhenAKeyIsSet()
    {
        using var factory = new UkweliApiFactory(anthropicApiKey: "sk-ant-not-a-real-key");
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<HealthResponse>(HealthPath);

        Assert.NotNull(body);
        Assert.True(body.ModelConfigured);
    }

    [Fact]
    public async Task NeverEchoesTheApiKey()
    {
        const string secret = "sk-ant-super-secret-value";
        using var factory = new UkweliApiFactory(anthropicApiKey: secret);
        using var client = factory.CreateClient();

        var raw = await client.GetStringAsync(HealthPath);

        Assert.DoesNotContain(secret, raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsDegradedWhenTheDatabaseIsUnreachable()
    {
        // Port 1 has nothing listening: the check must report the failure
        // rather than throwing, and the endpoint must say 503 so `curl --fail`
        // and container orchestrators both see it.
        using var factory = new UkweliApiFactory(
            databaseUrl: "postgres://ukweli:ukweli@localhost:1/ukweli_test");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(HealthPath);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.Db);
        Assert.False(body.Ok);
    }
}
