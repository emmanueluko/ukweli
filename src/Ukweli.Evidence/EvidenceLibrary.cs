namespace Ukweli.Evidence;

/// <summary>
/// Marker for the evidence library: the curated store, retrieval, and every
/// guardrail from section 2 of the specification.
/// </summary>
/// <remarks>
/// <para>
/// This project references <c>Ukweli.Contracts</c> and nothing else. It must
/// never reference ASP.NET Core, EF Core or Npgsql — the safety rules have to
/// be testable without a server or a database, and keeping the reference list
/// empty makes that a build error rather than a code-review note.
/// </para>
/// <para>
/// Phase 0 establishes the project. The curated store lands in Phase 1, the
/// seeded analyses in Phase 2, and retrieval plus the guardrails in Phase 3.
/// </para>
/// </remarks>
public static class EvidenceLibrary
{
    /// <summary>Directory holding the curated corpus, relative to the project root.</summary>
    public const string DataDirectoryName = "data";
}
