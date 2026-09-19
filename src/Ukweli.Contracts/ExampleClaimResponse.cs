namespace Ukweli.Contracts;

/// <summary>
/// One of the ready-made claims a user can pick instead of typing, as
/// <c>GET /api/examples</c> returns it.
/// </summary>
/// <param name="Id">Passed back as <c>exampleId</c> to analyze this claim.</param>
/// <param name="Claim">The claim text, shown on the button.</param>
/// <param name="Topic">The civic area it belongs to.</param>
/// <param name="Jurisdiction">Where it applies, when that is known.</param>
public sealed record ExampleClaimResponse(
    string Id,
    string Claim,
    Topic Topic,
    Jurisdiction? Jurisdiction);
