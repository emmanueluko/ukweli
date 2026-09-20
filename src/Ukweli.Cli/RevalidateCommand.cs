using Microsoft.EntityFrameworkCore;
using Ukweli.Contracts;
using Ukweli.Data;
using Ukweli.Evidence.Ingestion;

namespace Ukweli.Cli;

/// <summary>
/// Re-checks stored sources against the documents they cite.
/// </summary>
/// <remarks>
/// <para>
/// External pages change, move and are withdrawn. A store that never re-checks
/// slowly fills with excerpts that no longer exist anywhere, while continuing
/// to present them as what an authority currently says.
/// </para>
/// <para>
/// An automated record whose excerpt has gone is deactivated: what went in by
/// machine comes out by machine, without waiting for anyone to notice. A human
/// record is only ever reported, never deactivated — somebody chose it, and a
/// crawler that cannot reach a page today is not grounds for overruling them.
/// </para>
/// </remarks>
public static class RevalidateCommand
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var dryRun = CommandOptions.HasFlag(args, "--dry-run");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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

        var sources = await db.Sources
            .Where(source => source.Active)
            .OrderBy(source => source.Id)
            .ToListAsync(cancellationToken);

        using var http = new HttpClient { Timeout = DocumentFetcher.Timeout };
        // Identifies Ukweli while looking enough like a browser to be served:
        // NAFDAC answers 406 to a bare tool name. The contact URL stays so an
        // administrator can see who is asking and ask us to stop.
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; Ukweli/1.0; +https://www.ukweli.online)");
        var fetcher = new DocumentFetcher(http);

        var confirmed = 0;
        var deactivated = 0;
        var flagged = 0;

        foreach (var source in sources)
        {
            var document = await fetcher.FetchAsync(source.Url, cancellationToken);
            var stillThere = document is not null
                && IngestGate.ExcerptAppearsInDocument(source.Excerpt, document.Text);

            if (stillThere)
            {
                source.RevalidatedAt = today;
                source.CheckedAt = today;
                confirmed++;
                continue;
            }

            var reason = document is null ? "unreachable" : "excerpt no longer present";

            if (source.Curation == Curation.Automated)
            {
                Console.WriteLine($"  deactivate {source.Id}: {reason}");
                source.Active = false;
                deactivated++;
            }
            else
            {
                // Reported, not touched. A person put this here.
                Console.WriteLine($"  REVIEW     {source.Id}: {reason} (human-curated, left active)");
                flagged++;
            }
        }

        if (!dryRun)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        Console.WriteLine();
        Console.WriteLine(
            $"{sources.Count} checked: {confirmed} confirmed, {deactivated} deactivated, "
            + $"{flagged} flagged for review.{(dryRun ? " (dry run, nothing saved)" : string.Empty)}");

        // Flagged human records are for a person to resolve, not a reason to
        // fail the job and have the failure ignored.
        return CommandRunner.Success;
    }
}
