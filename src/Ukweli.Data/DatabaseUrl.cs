using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace Ukweli.Data;

/// <summary>
/// Translates the <c>DATABASE_URL</c> the spec documents into the form Npgsql
/// expects.
/// </summary>
/// <remarks>
/// <c>.env.example</c> documents <c>DATABASE_URL</c> in URL form
/// (<c>postgres://user:pass@host:port/db</c>), which is what the Postgres
/// ecosystem and every hosting provider hands you. Npgsql wants
/// <c>Host=…;Port=…;Database=…;Username=…;Password=…</c>. Rather than change the
/// documented variable, both forms are accepted here.
/// </remarks>
public static class DatabaseUrl
{
    private static readonly string[] UrlSchemes = ["postgres", "postgresql"];

    /// <summary>
    /// Converts <paramref name="value"/> to an Npgsql connection string. A value
    /// that is already an Npgsql connection string is returned normalised.
    /// </summary>
    /// <exception cref="ArgumentException">The value is blank or unparseable.</exception>
    public static string ToConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "DATABASE_URL is not set. Copy .env.example to .env and fill it in.",
                nameof(value));
        }

        var trimmed = value.Trim();

        return TryParseUrl(trimmed, out var fromUrl)
            ? fromUrl
            : NormaliseNpgsqlString(trimmed);
    }

    private static bool TryParseUrl(string value, [NotNullWhen(true)] out string? connectionString)
    {
        connectionString = null;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !UrlSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            // Uri.AbsolutePath is "/ukweli"; the database name is what follows the slash.
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        };

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length > 0 && userInfo[0].Length > 0)
        {
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
        }

        if (userInfo.Length == 2 && userInfo[1].Length > 0)
        {
            builder.Password = Uri.UnescapeDataString(userInfo[1]);
        }

        if (string.IsNullOrEmpty(builder.Database))
        {
            throw new ArgumentException(
                $"DATABASE_URL '{Redact(value)}' names no database.", nameof(value));
        }

        // Query parameters such as ?sslmode=require are passed straight through.
        foreach (var (key, parameterValue) in ParseQuery(uri.Query))
        {
            builder[key] = parameterValue;
        }

        connectionString = builder.ConnectionString;
        return true;
    }

    private static string NormaliseNpgsqlString(string value)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(value);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new ArgumentException(
                $"DATABASE_URL '{Redact(value)}' is neither a postgres:// URL nor an Npgsql connection string.",
                nameof(value),
                ex);
        }

        if (string.IsNullOrEmpty(builder.Host))
        {
            throw new ArgumentException(
                $"DATABASE_URL '{Redact(value)}' names no host.", nameof(value));
        }

        return builder.ConnectionString;
    }

    private static IEnumerable<(string Key, string Value)> ParseQuery(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            yield break;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                yield return (Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1]));
            }
        }
    }

    /// <summary>Strips credentials so a bad value can be reported without leaking a password.</summary>
    private static string Redact(string value)
    {
        var at = value.LastIndexOf('@');
        var schemeEnd = value.IndexOf("//", StringComparison.Ordinal);
        return at > 0 && schemeEnd > 0 && at > schemeEnd
            ? string.Concat(value.AsSpan(0, schemeEnd + 2), "***", value.AsSpan(at))
            : "***";
    }
}
