using Ukweli.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddUkweliServices();

var app = builder.Build();
app.MapUkweliEndpoints();

await app.RunAsync();

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> in the test project can
/// boot the real application rather than a stand-in.
/// </summary>
public partial class Program;
