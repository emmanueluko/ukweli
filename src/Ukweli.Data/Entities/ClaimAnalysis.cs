using Ukweli.Contracts;

namespace Ukweli.Data.Entities;

/// <summary>
/// One stored result, mapped to the <c>claim_analyses</c> table.
/// </summary>
/// <remarks>
/// There is deliberately no column for the raw text a user pasted. Only
/// <see cref="NormalizedClaim"/> is kept, and claim text is never written to a
/// log — analysis ids and timings are.
/// </remarks>
public class ClaimAnalysis
{
    /// <summary>nanoid(10), also the <c>/r/{id}</c> share path.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Set when an authenticated user ran the check (Phase 4).</summary>
    public string? UserId { get; set; }

    /// <summary>The claim as the system understood it. Never the raw input.</summary>
    public string NormalizedClaim { get; set; } = string.Empty;

    public Jurisdiction? Jurisdiction { get; set; }

    public VerdictStatus Status { get; set; }

    public string Explanation { get; set; } = string.Empty;

    /// <summary>The same evidence in shorter words. Never new facts.</summary>
    public string SimpleExplanation { get; set; } = string.Empty;

    public List<string> Unknowns { get; set; } = [];

    public string Action { get; set; } = string.Empty;

    /// <summary>True when the action is the fixed caution rather than a grounded step.</summary>
    public bool ActionIsGeneric { get; set; }

    /// <summary>The cited source ids, in the order they were cited.</summary>
    public List<string> SourceIds { get; set; } = [];

    /// <summary>
    /// How each cited source stands to the claim, keyed by source id.
    /// </summary>
    /// <remarks>
    /// Held separately from <see cref="SourceIds"/>, which the specification
    /// defines as a plain string array. A single status cannot express a mixed
    /// verdict where one source supports and another conflicts, so the relation
    /// is recorded per source rather than inferred from the verdict on read.
    /// </remarks>
    public Dictionary<string, SourceRelation> SourceRelations { get; set; } = [];

    /// <summary>True for a hand-written analysis; such claims never reach the model.</summary>
    public bool IsSeeded { get; set; }

    /// <summary>The seed this came from, when it is a seeded claim.</summary>
    public string? SeedId { get; set; }

    /// <summary><c>provider/model@prompt-vN</c>, or a deterministic marker for seeds.</summary>
    public string ModelVersion { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
