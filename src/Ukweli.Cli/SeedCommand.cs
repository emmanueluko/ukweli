using Microsoft.EntityFrameworkCore;
using Ukweli.Data;
using Ukweli.Data.Entities;
using Ukweli.Evidence;

namespace Ukweli.Cli;

/// <summary>
/// Loads the curated store into Postgres.
/// </summary>
/// <remarks>
/// Idempotent: running it twice leaves the same rows. Records are matched by
/// id, existing rows are updated in place, and a row whose id has disappeared
/// from the corpus is deactivated rather than deleted, so an analysis stored
/// earlier can still resolve the source it cited.
/// </remarks>
public static class SeedCommand
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var path = CommandOptions.ResolveSourcesPath(args);

        IReadOnlyList<SourceRecord> records;
        try
        {
            records = SourceMapping.Normalise(SourceCorpus.LoadFromFile(path));
        }
        catch (SourceCorpusException ex)
        {
            Console.Error.WriteLine($"  [corpus] {ex.Message}");
            return CommandRunner.Failure;
        }

        // Structural problems are fatal; an uncurated placeholder is not. Seeding
        // a placeholder is how a fresh clone gets a working demo path, and
        // `verify-sources` remains the gate that refuses to call it ready.
        var blocking = SourceValidator.Validate(records)
            .Where(problem => problem.Code != SourceProblemCodes.Placeholder)
            .ToList();

        if (blocking.Count > 0)
        {
            Console.Error.WriteLine($"Refusing to seed: {blocking.Count} problem(s).");
            foreach (var problem in blocking)
            {
                Console.Error.WriteLine($"  {problem}");
            }

            return CommandRunner.Failure;
        }

        string connectionString;
        try
        {
            connectionString = DatabaseUrl.ToConnectionString(Configuration.DatabaseUrl());
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return CommandRunner.Failure;
        }

        var options = new DbContextOptionsBuilder<UkweliDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new UkweliDbContext(options);

        var (inserted, updated, deactivated) = await ApplyAsync(db, records, cancellationToken);

        Console.WriteLine(
            $"Seeded {records.Count} source(s): {inserted} inserted, {updated} updated, "
            + $"{deactivated} deactivated.");

        var placeholders = records.Count(SourceValidator.LooksUncurated);
        if (placeholders > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"{placeholders} of them are placeholders and are not real evidence. "
                + "`make verify-sources` will fail until a human curates them.");
        }

        return CommandRunner.Success;
    }

    internal static async Task<(int Inserted, int Updated, int Deactivated)> ApplyAsync(
        UkweliDbContext db,
        IReadOnlyList<SourceRecord> records,
        CancellationToken cancellationToken)
    {
        var existing = await db.Sources.ToDictionaryAsync(source => source.Id, cancellationToken);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var inserted = 0;
        var updated = 0;

        foreach (var record in records)
        {
            seenIds.Add(record.Id);

            if (existing.TryGetValue(record.Id, out var row))
            {
                Populate(row, record);
                updated++;
            }
            else
            {
                var fresh = new Source { Id = record.Id };
                Populate(fresh, record);
                db.Sources.Add(fresh);
                inserted++;
            }
        }

        var deactivated = 0;
        foreach (var (id, row) in existing)
        {
            // Never deleted: an analysis stored earlier cites this id, and that
            // result has to stay readable.
            if (!seenIds.Contains(id) && row.Active)
            {
                row.Active = false;
                deactivated++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return (inserted, updated, deactivated);
    }

    private static void Populate(Source row, SourceRecord record)
    {
        row.Issuer = record.Issuer;
        row.Title = record.Title;
        row.SourceType = record.SourceType;
        row.Jurisdiction = record.Jurisdiction;
        row.Topic = record.Topic;
        row.PublishedAt = record.PublishedAt;
        row.CheckedAt = record.CheckedAt;
        row.Url = record.Url;
        row.CollectionUrl = record.CollectionUrl;
        row.Excerpt = record.Excerpt;
        row.Placeholder = record.Placeholder;
        row.Active = record.Active;
    }
}
