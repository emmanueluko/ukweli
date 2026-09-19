using Ukweli.Evidence;

namespace Ukweli.Cli;

/// <summary>Shared argument parsing for the commands.</summary>
internal static class CommandOptions
{
    /// <summary>
    /// Resolves the corpus path: an explicit <c>--sources</c> argument, else the
    /// copy the build places beside the executable.
    /// </summary>
    public static string ResolveSourcesPath(string[] args) =>
        ValueOf(args, "--sources") ?? SourceCorpus.DefaultPath();

    public static bool HasFlag(string[] args, string flag) =>
        args.Contains(flag, StringComparer.Ordinal);

    public static string? ValueOf(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
