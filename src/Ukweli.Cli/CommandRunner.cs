namespace Ukweli.Cli;

/// <summary>
/// The operational commands: seeding the curated store into Postgres, and
/// verifying that store before it can be seeded.
/// </summary>
/// <remarks>
/// These live outside <c>Ukweli.Evidence</c> because they do I/O — they read
/// files, reach the network, and write to a database. The evidence library
/// stays pure so its guardrails can be tested without any of that.
/// </remarks>
public static class CommandRunner
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int UsageError = 2;

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        var command = args.Length > 0 ? args[0] : null;
        var rest = args.Length > 1 ? args[1..] : [];

        return command switch
        {
            "seed" => await SeedCommand.RunAsync(rest, cancellationToken),
            "verify-sources" => await VerifySourcesCommand.RunAsync(rest, cancellationToken),
            "--help" or "-h" or "help" or null => PrintUsage(Success),
            _ => PrintUnknown(command),
        };
    }

    private static int PrintUnknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        Console.Error.WriteLine();
        return PrintUsage(UsageError);
    }

    private static int PrintUsage(int exitCode)
    {
        var writer = exitCode == Success ? Console.Out : Console.Error;

        writer.WriteLine("Usage: ukweli <command> [options]");
        writer.WriteLine();
        writer.WriteLine("Commands:");
        writer.WriteLine("  verify-sources   Validate the curated store. Fails while any record is a");
        writer.WriteLine("                   placeholder, an id is duplicated, or a url is unreachable.");
        writer.WriteLine("  seed             Load the curated store into the database (idempotent).");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --sources <path> Path to sources.json. Defaults to the copy beside this build.");
        writer.WriteLine("  --skip-http      verify-sources only: skip reachability checks (offline runs).");
        writer.WriteLine();
        writer.WriteLine("Environment:");
        writer.WriteLine("  DATABASE_URL     Required by seed. postgres:// URL or an Npgsql string.");

        return exitCode;
    }
}
