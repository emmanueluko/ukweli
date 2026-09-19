using Ukweli.Evidence;

namespace Ukweli.Cli;

/// <summary>
/// The gate that stops an uncurated corpus reaching a demo.
/// </summary>
/// <remarks>
/// Safety rule 9: a record carrying <c>"placeholder": true</c> must fail this
/// command until a human replaces it. That failure is a feature — do not remove
/// it, and do not add a flag that skips it.
/// </remarks>
public static class VerifySourcesCommand
{
    /// <summary>Generous: these are static government pages, not an API.</summary>
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var path = CommandOptions.ResolveSourcesPath(args);
        var skipHttp = CommandOptions.HasFlag(args, "--skip-http");

        Console.WriteLine($"Verifying {path}");

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

        var problems = SourceValidator.Validate(records).ToList();

        if (skipHttp)
        {
            Console.WriteLine("  Skipping URL reachability checks (--skip-http).");
        }
        else
        {
            // Only records that already passed validation are worth a request;
            // a placeholder's url is not an address.
            var checkable = records
                .Where(record => !SourceValidator.LooksUncurated(record))
                .Where(record => SourceValidator.IsHttpUrl(record.Url))
                .ToList();

            problems.AddRange(await CheckReachabilityAsync(checkable, cancellationToken));
        }

        return Report(records.Count, problems);
    }

    private static async Task<IReadOnlyList<SourceProblem>> CheckReachabilityAsync(
        List<SourceRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        using var client = new HttpClient { Timeout = HttpTimeout };
        // Some government sites reject requests without a browser-like agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Ukweli-verify-sources/1.0");

        var checks = records.Select(record => CheckOneAsync(client, record, cancellationToken));
        var results = await Task.WhenAll(checks);

        return [.. results.OfType<SourceProblem>()];
    }

    private static async Task<SourceProblem?> CheckOneAsync(
        HttpClient client, SourceRecord record, CancellationToken cancellationToken)
    {
        try
        {
            // HEAD first: these are often large PDFs and the body is not wanted.
            using var head = new HttpRequestMessage(HttpMethod.Head, record.Url);
            using var response = await client.SendAsync(head, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"  ok  {record.Id} -> {(int)response.StatusCode}");
                return null;
            }

            // Plenty of servers refuse HEAD but answer GET perfectly well.
            using var get = new HttpRequestMessage(HttpMethod.Get, record.Url);
            using var fallback = await client.SendAsync(
                get, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (fallback.IsSuccessStatusCode)
            {
                Console.WriteLine($"  ok  {record.Id} -> {(int)fallback.StatusCode} (GET)");
                return null;
            }

            return new SourceProblem(
                record.Id,
                SourceProblemCodes.UnreachableUrl,
                $"{record.Url} answered {(int)fallback.StatusCode} {fallback.ReasonPhrase}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new SourceProblem(
                record.Id,
                SourceProblemCodes.UnreachableUrl,
                $"{record.Url} could not be reached: {ex.Message}");
        }
    }

    private static int Report(int recordCount, List<SourceProblem> problems)
    {
        Console.WriteLine();

        if (problems.Count == 0)
        {
            Console.WriteLine($"{recordCount} source(s) verified. The curated store is fit to seed.");
            return CommandRunner.Success;
        }

        Console.Error.WriteLine($"{problems.Count} problem(s) in {recordCount} source(s):");
        Console.Error.WriteLine();

        foreach (var problem in problems.OrderBy(p => p.SourceId, StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"  {problem}");
        }

        if (problems.Any(p => p.Code == SourceProblemCodes.Placeholder))
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Placeholder records are expected in a fresh clone, and this failure is "
                + "deliberate: Ukweli must not serve evidence nobody has curated. "
                + "See src/Ukweli.Evidence/data/README.md for what to replace.");
        }

        return CommandRunner.Failure;
    }
}
