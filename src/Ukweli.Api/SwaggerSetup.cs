using Microsoft.OpenApi;

namespace Ukweli.Api;

/// <summary>
/// The interactive API explorer, for trying endpoints locally.
/// </summary>
/// <remarks>
/// Development only. The demo is a public claim checker with no admin surface,
/// so there is nothing to gain from publishing a schema in production and a
/// little to lose.
/// </remarks>
public static class SwaggerSetup
{
    private const string DocumentName = "v1";
    private const string DocumentPath = "/openapi/v1.json";

    public static void AddUkweliOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Ukweli API",
                    Version = "v1",
                    Description =
                        "Evidence-first civic claim checking for Nigeria. Claims are checked "
                        + "only against the curated store of verified primary sources; an "
                        + "unknown claim returns insufficient_evidence rather than a guess, "
                        + "and no response carries a confidence score.",
                };

                return Task.CompletedTask;
            });
        });
    }

    public static void MapUkweliOpenApi(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        app.MapOpenApi(DocumentPath);

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(DocumentPath, "Ukweli API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Ukweli API";
        });
    }
}
