using Ukweli.Data;

namespace Ukweli.Cli;

/// <summary>
/// Finds the commands' configuration the same way the API does.
/// </summary>
/// <remarks>
/// Without this, <c>make db-seed</c> would fail on a fresh clone unless the
/// operator happened to export DATABASE_URL by hand, even though they had
/// filled in the <c>.env</c> the README tells them to create. A real
/// environment variable still wins over the file.
/// </remarks>
internal static class Configuration
{
    public static string? DatabaseUrl() => Value("DATABASE_URL");

    private static string? Value(string key)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        // The working directory first, so running from the repository root
        // finds the operator's file; then beside the build, for a published
        // copy.
        var path = DotEnv.FindFile(Directory.GetCurrentDirectory())
            ?? DotEnv.FindFile(AppContext.BaseDirectory);

        return path is null ? null : DotEnv.Read(path).GetValueOrDefault(key);
    }
}
