using Ukweli.Contracts;

namespace Ukweli.Evidence;

/// <summary>
/// A hand-written analysis for a known claim, from <c>data/seeds.json</c>.
/// </summary>
/// <remarks>
/// Seeded claims never reach the model. They are what makes the deterministic
/// slice demonstrable on its own, and what gives the three verdict paths a
/// fixture that cannot drift.
/// </remarks>
public sealed record SeedRecord
{
    /// <summary>Stable id, offered to clients as <c>exampleId</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The claim as a person would paste it. Matched against user input.</summary>
    public required string Claim { get; init; }

    /// <summary>The claim as the system records it. This is what gets stored.</summary>
    public required string NormalizedClaim { get; init; }

    public Jurisdiction? Jurisdiction { get; init; }

    public required Topic Topic { get; init; }

    public required VerdictStatus Status { get; init; }

    public required string Explanation { get; init; }

    public required string SimpleExplanation { get; init; }

    public IReadOnlyList<string> Unknowns { get; init; } = [];

    /// <summary>
    /// A next step grounded in a cited excerpt. Empty means the fixed generic
    /// caution is used instead, and <see cref="ActionIsGeneric"/> says so.
    /// </summary>
    public string Action { get; init; } = string.Empty;

    public bool ActionIsGeneric { get; init; }

    public IReadOnlyList<SeedSource> Sources { get; init; } = [];

    /// <summary>True while the analysis text is still awaiting a curator.</summary>
    public bool Placeholder { get; init; }
}

/// <summary>A source cited by a seeded analysis, and how it stands to the claim.</summary>
public sealed record SeedSource
{
    public required string Id { get; init; }

    public required SourceRelation Relation { get; init; }
}
