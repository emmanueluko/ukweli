using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ukweli.Evidence;

/// <summary>
/// Reads the curated store from <c>data/sources.json</c>.
/// </summary>
/// <remarks>
/// Parsing only. Whether the contents are fit to seed is
/// <see cref="SourceValidator"/>'s decision, and reaching the network to check
/// a URL belongs outside this project entirely — nothing here does I/O beyond
/// reading the file it is handed.
/// </remarks>
public static class SourceCorpus
{
    /// <summary>The corpus is deliberately small; retrieval is keyword scoring, not search.</summary>
    public const int MaxRecords = 15;

    public const string FileName = "sources.json";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Parses corpus JSON.</summary>
    /// <exception cref="SourceCorpusException">The JSON is malformed or not an array of records.</exception>
    public static IReadOnlyList<SourceRecord> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        List<SourceRecord>? records;
        try
        {
            records = JsonSerializer.Deserialize<List<SourceRecord>>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new SourceCorpusException($"{FileName} is not valid JSON: {ex.Message}", ex);
        }

        return records is null
            ? throw new SourceCorpusException($"{FileName} must contain an array of source records.")
            : records;
    }

    public static IReadOnlyList<SourceRecord> LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new SourceCorpusException($"No curated store found at {path}.");
        }

        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Locates <c>data/sources.json</c> next to the running assembly, where the
    /// build copies it.
    /// </summary>
    public static string DefaultPath(string? baseDirectory = null) =>
        Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "data", FileName);
}

public sealed class SourceCorpusException : Exception
{
    public SourceCorpusException(string message) : base(message) { }

    public SourceCorpusException(string message, Exception innerException)
        : base(message, innerException) { }

    public SourceCorpusException() { }
}
