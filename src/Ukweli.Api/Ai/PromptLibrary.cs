namespace Ukweli.Api.Ai;

/// <summary>
/// Loads the versioned prompt templates from <c>prompts/</c>.
/// </summary>
/// <remarks>
/// A prompt is never edited in place once it has produced stored analyses —
/// a new version is added instead, so the <c>modelVersion</c> recorded on each
/// row keeps pointing at the text that actually ran.
/// </remarks>
public sealed class PromptLibrary
{
    public const string ExtractVersion = "extract-v1";
    public const string VerdictVersion = "verdict-v1";

    public PromptLibrary(string? promptsDirectory = null)
    {
        Directory = promptsDirectory ?? Locate();
        Extract = Read("extract.v1.txt");
        Verdict = Read("verdict.v1.txt");
    }

    public string Directory { get; }

    public string Extract { get; }

    public string Verdict { get; }

    private string Read(string fileName)
    {
        var path = Path.Combine(Directory, fileName);

        return File.Exists(path)
            ? File.ReadAllText(path)
            : throw new FileNotFoundException(
                $"Prompt template not found at {path}. The prompts/ directory must ship with the build.",
                path);
    }

    /// <summary>
    /// Finds <c>prompts/</c> beside the build first, then by walking up to the
    /// repository root, so both a published container and `dotnet run` work.
    /// </summary>
    private static string Locate()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "prompts");
        if (System.IO.Directory.Exists(beside))
        {
            return beside;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        for (var depth = 0; depth < 8 && directory is not null; depth++)
        {
            var candidate = Path.Combine(directory.FullName, "prompts");
            if (System.IO.Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return beside;
    }

    /// <summary>Fills the placeholders in a template. Values are data, never instructions.</summary>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        var filled = template;

        foreach (var (key, value) in values)
        {
            filled = filled.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal);
        }

        return filled;
    }
}
