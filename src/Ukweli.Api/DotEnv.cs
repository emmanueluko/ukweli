namespace Ukweli.Api;

/// <summary>
/// Reads a <c>.env</c> file into a dictionary of configuration values.
/// </summary>
/// <remarks>
/// <para>
/// The spec's configuration contract is a <c>.env</c> file, which .NET does not
/// read natively. This is a deliberately small reader rather than a package:
/// it handles <c>KEY=value</c>, <c>#</c> comments, <c>export</c> prefixes and
/// quoted values, which is everything <c>.env.example</c> uses.
/// </para>
/// <para>
/// It returns values rather than calling
/// <see cref="Environment.SetEnvironmentVariable(string, string)"/>. Mutating
/// the process environment would make the file win over a real deployment's
/// variables and leak between hosts inside one test process; as a configuration
/// source the caller decides its priority, and <see cref="ApplicationSetup"/>
/// gives it the lowest.
/// </para>
/// </remarks>
public static class DotEnv
{
    public const string FileName = ".env";

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for a
    /// <c>.env</c> file. Running from <c>bin/Debug/net10.0</c> is why this
    /// searches upwards.
    /// </summary>
    /// <returns>The path of the first file found, or null.</returns>
    public static string? FindFile(string startDirectory, int maxDepth = 8)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var directory = new DirectoryInfo(startDirectory);

        for (var depth = 0; depth < maxDepth && directory is not null; depth++)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>Parses a <c>.env</c> file. A later line wins over an earlier one.</summary>
    public static IReadOnlyDictionary<string, string?> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(path))
        {
            if (TryParseLine(line, out var key, out var value))
            {
                values[key] = value;
            }
        }

        return values;
    }

    internal static bool TryParseLine(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return false;
        }

        if (trimmed.StartsWith("export ", StringComparison.Ordinal))
        {
            trimmed = trimmed["export ".Length..].TrimStart();
        }

        var separator = trimmed.IndexOf('=');
        if (separator <= 0)
        {
            return false;
        }

        key = trimmed[..separator].TrimEnd();
        if (key.Length == 0)
        {
            return false;
        }

        value = Unquote(trimmed[(separator + 1)..].Trim());
        return true;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        // An unquoted value may carry a trailing comment: KEY=value # note
        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        return comment >= 0 ? value[..comment].TrimEnd() : value;
    }
}
