using Ukweli.Data;

namespace Ukweli.Api.Tests;

/// <summary>
/// <c>.env.example</c> documents DATABASE_URL in postgres:// form; Npgsql wants
/// key/value pairs. Both forms have to work, and a bad value has to fail with a
/// message that does not leak the password.
/// </summary>
public class DatabaseUrlTests
{
    [Fact]
    public void ParsesTheUrlFormFromEnvExample()
    {
        var result = DatabaseUrl.ToConnectionString("postgres://ukweli:ukweli@localhost:5432/ukweli");

        Assert.Contains("Host=localhost", result, StringComparison.Ordinal);
        Assert.Contains("Port=5432", result, StringComparison.Ordinal);
        Assert.Contains("Database=ukweli", result, StringComparison.Ordinal);
        Assert.Contains("Username=ukweli", result, StringComparison.Ordinal);
        Assert.Contains("Password=ukweli", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsThePostgresqlScheme()
    {
        var result = DatabaseUrl.ToConnectionString("postgresql://u:p@db.example.com:6543/ukweli");

        Assert.Contains("Host=db.example.com", result, StringComparison.Ordinal);
        Assert.Contains("Port=6543", result, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultsToPort5432WhenTheUrlOmitsIt()
    {
        var result = DatabaseUrl.ToConnectionString("postgres://ukweli:ukweli@localhost/ukweli");

        Assert.Contains("Port=5432", result, StringComparison.Ordinal);
    }

    [Fact]
    public void PassesQueryParametersThrough()
    {
        var result = DatabaseUrl.ToConnectionString(
            "postgres://u:p@host:5432/ukweli?sslmode=require");

        Assert.Contains("SSL Mode=Require", result, StringComparison.Ordinal);
    }

    [Fact]
    public void DecodesPercentEncodedCredentials()
    {
        // A password containing '@' or '/' must survive the round trip.
        var result = DatabaseUrl.ToConnectionString("postgres://user:p%40ss%2Fword@host:5432/ukweli");

        Assert.Contains("p@ss/word", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsANativeNpgsqlConnectionString()
    {
        var result = DatabaseUrl.ToConnectionString(
            "Host=localhost;Port=5432;Database=ukweli;Username=ukweli;Password=ukweli");

        Assert.Contains("Host=localhost", result, StringComparison.Ordinal);
        Assert.Contains("Database=ukweli", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsAnUnsetValueWithAnActionableMessage(string? value)
    {
        var exception = Assert.Throws<ArgumentException>(() => DatabaseUrl.ToConnectionString(value));

        Assert.Contains("DATABASE_URL is not set", exception.Message, StringComparison.Ordinal);
        Assert.Contains(".env", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAUrlThatNamesNoDatabase()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => DatabaseUrl.ToConnectionString("postgres://ukweli:ukweli@localhost:5432"));

        Assert.Contains("names no database", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotLeakThePasswordWhenReportingABadUrl()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => DatabaseUrl.ToConnectionString("postgres://ukweli:hunter2@localhost:5432"));

        Assert.DoesNotContain("hunter2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotLeakThePasswordWhenReportingAnUnparseableValue()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => DatabaseUrl.ToConnectionString("Host=;Password=hunter2"));

        Assert.DoesNotContain("hunter2", exception.Message, StringComparison.Ordinal);
    }
}
