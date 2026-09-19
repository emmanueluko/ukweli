using Microsoft.EntityFrameworkCore;
using Ukweli.Api;
using Ukweli.Data;

var builder = WebApplication.CreateBuilder(args);
builder.AddUkweliServices();

var app = builder.Build();

// The container entrypoint runs migrations as a separate step before starting
// the server, so a failed migration stops the container instead of leaving an
// API serving against a schema it does not match.
if (args.Contains("--migrate-only", StringComparer.Ordinal))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<UkweliDbContext>().Database.MigrateAsync();
    return;
}

app.MapUkweliEndpoints();

await app.RunAsync();

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> in the test project can
/// boot the real application rather than a stand-in.
/// </summary>
public partial class Program;
