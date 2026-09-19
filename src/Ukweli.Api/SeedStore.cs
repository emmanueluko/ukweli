using Ukweli.Evidence;

namespace Ukweli.Api;

/// <summary>
/// Holds the seeded analyses in memory.
/// </summary>
/// <remarks>
/// The file is read once at startup: it is three records that ship with the
/// build and cannot change while the process runs, so re-reading it per request
/// would buy nothing.
/// </remarks>
public sealed class SeedStore
{
    public SeedStore(string? path = null)
    {
        Path = path ?? SeedCorpus.DefaultPath();
        Records = File.Exists(Path) ? SeedCorpus.LoadFromFile(Path) : [];
    }

    public string Path { get; }

    public IReadOnlyList<SeedRecord> Records { get; }

    /// <summary>The seeds offered on the home screen, as the examples endpoint returns them.</summary>
    public IReadOnlyList<Contracts.ExampleClaimResponse> Examples =>
        [.. Records.Select(seed => new Contracts.ExampleClaimResponse(
            seed.Id, seed.Claim, seed.Topic, seed.Jurisdiction))];
}
