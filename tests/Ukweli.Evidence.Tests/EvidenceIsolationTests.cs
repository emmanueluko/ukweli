using System.Reflection;
using Ukweli.Evidence;

namespace Ukweli.Evidence.Tests;

/// <summary>
/// Safety rule 1 and the layout rule behind it: the evidence library holds every
/// guardrail and must stay testable without a server or a database. These tests
/// fail the build if someone later adds a web or data dependency to it.
/// </summary>
public class EvidenceIsolationTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.Extensions.Hosting",
    ];

    [Fact]
    public void EvidenceAssembly_ReferencesNoHttpOrDatabasePackage()
    {
        var assembly = typeof(EvidenceLibrary).Assembly;

        var offenders = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => ForbiddenAssemblyPrefixes.Any(
                prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Ukweli.Evidence must contain no HTTP or database dependency, but references: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void EvidenceAssembly_ReferencesNoProjectAssemblyBeyondContracts()
    {
        var assembly = typeof(EvidenceLibrary).Assembly;

        // A subset check, not an equality check: the compiler omits a reference
        // the code does not actually use, so Ukweli.Contracts is absent until
        // the evidence types start using it in Phase 1. What must never appear
        // is Ukweli.Api or Ukweli.Data.
        var unexpected = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("Ukweli.", StringComparison.Ordinal))
            .Where(name => !string.Equals(name, "Ukweli.Contracts", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            unexpected.Length == 0,
            $"Ukweli.Evidence may reference only Ukweli.Contracts, but also references: {string.Join(", ", unexpected)}");
    }
}
