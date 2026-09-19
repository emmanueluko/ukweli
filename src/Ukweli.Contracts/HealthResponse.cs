namespace Ukweli.Contracts;

/// <param name="Ok">True when every dependency the API needs is reachable.</param>
/// <param name="Db">
/// The result of a live <c>SELECT 1</c>, not a cached flag — otherwise the
/// check would report a healthy API against a dead database, which is the exact
/// failure it exists to catch.
/// </param>
/// <param name="ModelConfigured">
/// Whether an Anthropic API key is present. This never calls the model and
/// never reveals the key.
/// </param>
public sealed record HealthResponse(bool Ok, bool Db, bool ModelConfigured);
