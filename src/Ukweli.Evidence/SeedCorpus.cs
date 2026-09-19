using System.Text.Json;

namespace Ukweli.Evidence;

/// <summary>Reads the seeded analyses from <c>data/seeds.json</c>.</summary>
public static class SeedCorpus
{
    public const string FileName = "seeds.json";

    /// <exception cref="SourceCorpusException">The JSON is malformed or not an array.</exception>
    public static IReadOnlyList<SeedRecord> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        List<SeedRecord>? records;
        try
        {
            records = JsonSerializer.Deserialize<List<SeedRecord>>(json, SourceCorpus.JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new SourceCorpusException($"{FileName} is not valid JSON: {ex.Message}", ex);
        }

        return records is null
            ? throw new SourceCorpusException($"{FileName} must contain an array of seeded analyses.")
            : records;
    }

    public static IReadOnlyList<SeedRecord> LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return File.Exists(path)
            ? Parse(File.ReadAllText(path))
            : throw new SourceCorpusException($"No seeded analyses found at {path}.");
    }

    public static string DefaultPath(string? baseDirectory = null) =>
        Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "data", FileName);

    /// <summary>
    /// Finds the seed whose claim matches <paramref name="text"/>, or null.
    /// </summary>
    /// <remarks>
    /// Matching is on the normalised form of both sides, so casing, spacing and
    /// surrounding punctuation do not decide whether a demo works.
    /// </remarks>
    public static SeedRecord? MatchByText(IReadOnlyList<SeedRecord> seeds, string? text)
    {
        ArgumentNullException.ThrowIfNull(seeds);

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalised = ClaimNormaliser.Normalise(text);

        return seeds.FirstOrDefault(seed =>
            ClaimNormaliser.Matches(seed.Claim, normalised) ||
            ClaimNormaliser.Matches(seed.NormalizedClaim, normalised));
    }

    public static SeedRecord? MatchById(IReadOnlyList<SeedRecord> seeds, string? exampleId)
    {
        ArgumentNullException.ThrowIfNull(seeds);

        return string.IsNullOrWhiteSpace(exampleId)
            ? null
            : seeds.FirstOrDefault(seed =>
                string.Equals(seed.Id, exampleId.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
