using Microsoft.EntityFrameworkCore;
using Ukweli.Contracts;
using Ukweli.Data;
using Ukweli.Data.Entities;
using Ukweli.Evidence;
using Ukweli.Evidence.Ingestion;

namespace Ukweli.Cli;

/// <summary>
/// Keeps the curated store current by fetching new documents from the
/// publishers in the registry.
/// </summary>
/// <remarks>
/// <para>
/// Every record this writes is marked <see cref="Curation.Automated"/> and had
/// its excerpt verified as a literal passage of the document at the URL it
/// cites. Nothing is summarised, and nothing is written for a publisher, topic
/// or document type the registry does not name.
/// </para>
/// <para>
/// It only ever adds. A record a person curated is never modified or removed
/// by this command, because a machine that can overwrite human judgement is a
/// machine that can erase it.
/// </para>
/// </remarks>
public static class IngestCommand
{
    /// <summary>Per publisher, per run. Ingesting slowly is fine; the store is cumulative.</summary>
    private const int MaxNewPerPublisher = 5;

    /// <summary>Candidate links examined per collection page.</summary>
    private const int MaxCandidatesPerCollection = 25;

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

        var known = await db.Sources
            .Select(source => source.Url)
            .ToListAsync(cancellationToken);
        var seen = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);

        using var http = new HttpClient { Timeout = DocumentFetcher.Timeout };
        // Identifies Ukweli while looking enough like a browser to be served:
        // NAFDAC answers 406 to a bare tool name. The contact URL stays so an
        // administrator can see who is asking and ask us to stop.
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; Ukweli/1.0; +https://www.ukweli.online)");
        var fetcher = new DocumentFetcher(http);

        var added = 0;
        var rejected = 0;

        foreach (var publisher in SourceRegistry.All)
        {
            var addedHere = 0;
            Console.WriteLine($"\n{publisher.Host} ({publisher.Issuer})");

            foreach (var collection in publisher.Collections)
            {
                if (addedHere >= MaxNewPerPublisher)
                {
                    break;
                }

                var links = await fetcher.DiscoverAsync(collection, publisher.Host, cancellationToken);
                Console.WriteLine($"  {collection} -> {links.Count} link(s)");

                foreach (var link in links.Take(MaxCandidatesPerCollection))
                {
                    if (addedHere >= MaxNewPerPublisher)
                    {
                        break;
                    }

                    if (!seen.Add(link))
                    {
                        continue;
                    }

                    var outcome = await ConsiderAsync(
                        fetcher, publisher, collection, link, today, cancellationToken);

                    if (outcome is null)
                    {
                        continue;
                    }

                    if (outcome.Problems.Count > 0)
                    {
                        rejected++;
                        Console.WriteLine($"    reject {Short(link)}: {outcome.Problems[0].Code}");
                        continue;
                    }

                    Console.WriteLine($"    ADD    {outcome.Record!.Id}");
                    addedHere++;
                    added++;

                    if (!dryRun)
                    {
                        db.Sources.Add(ToRow(outcome.Record, today));
                    }
                }
            }
        }

        if (!dryRun && added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        Console.WriteLine();
        Console.WriteLine(dryRun
            ? $"Dry run: {added} record(s) would be added, {rejected} rejected."
            : $"{added} record(s) added, {rejected} rejected.");

        // A run that adds nothing is normal — the publishers may simply have
        // published nothing — so it is not a failure.
        return CommandRunner.Success;
    }

    private sealed record Outcome(SourceRecord? Record, IReadOnlyList<SourceProblem> Problems);

    private static async Task<Outcome?> ConsiderAsync(
        DocumentFetcher fetcher,
        SourceRegistry.Publisher publisher,
        string collection,
        string url,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var document = await fetcher.FetchAsync(url, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var published = ExcerptChooser.FindPublicationDate(document.Text, today);
        if (published is null)
        {
            // No stated date means no record. Every verdict shows its source's
            // date, and inventing one would be inventing evidence.
            return null;
        }

        // A document may be on topic for more than one of a publisher's areas;
        // the first that yields a passage is the one it is filed under.
        foreach (var topic in publisher.Topics)
        {
            var excerpt = ExcerptChooser.Choose(document.Text, topic);
            if (excerpt is null)
            {
                continue;
            }

            var record = new SourceRecord
            {
                Id = ExcerptChooser.BuildId(publisher.Host, topic, published.Value, url),
                Issuer = publisher.Issuer,
                Title = ExcerptChooser.FindTitle(document.Text) ?? publisher.Issuer,
                SourceType = publisher.SourceType,
                Jurisdiction = publisher.Jurisdiction,
                Topic = topic,
                PublishedAt = published,
                CheckedAt = today,
                Url = url,
                CollectionUrl = collection,
                Excerpt = excerpt,
                Placeholder = false,
                Active = true,
                Curation = Curation.Automated,
                RevalidatedAt = today,
            };

            var problems = IngestGate.Check(new IngestGate.Submission(record, document.Text, today));
            return new Outcome(problems.Count == 0 ? record : null, problems);
        }

        return null;
    }

    private static Source ToRow(SourceRecord record, DateOnly today) => new()
    {
        Id = record.Id,
        Issuer = record.Issuer,
        Title = record.Title,
        SourceType = record.SourceType,
        Jurisdiction = record.Jurisdiction,
        Topic = record.Topic,
        PublishedAt = record.PublishedAt,
        CheckedAt = record.CheckedAt,
        Url = record.Url,
        CollectionUrl = record.CollectionUrl,
        Excerpt = record.Excerpt,
        Placeholder = false,
        Active = true,
        Curation = Curation.Automated,
        RevalidatedAt = today,
    };

    private static string Short(string url) => url.Length > 70 ? url[..70] + "…" : url;
}
