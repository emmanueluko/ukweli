using Anthropic;
using Microsoft.Extensions.Configuration.Memory;
using Ukweli.Api.Ai;
using Ukweli.Api.Auth;
using Ukweli.Evidence;
using Ukweli.Contracts;
using Ukweli.Data;

namespace Ukweli.Api;

public static class ApplicationSetup
{
    public static WebApplicationBuilder AddUkweliServices(this WebApplicationBuilder builder)
    {
        // A .env file is the spec's configuration contract, but only as a
        // fallback. Inserting it at position 0 makes it the lowest-priority
        // source, so a real environment variable — from a container, from CI,
        // or from a test host — always wins over the file on disk.
        if (DotEnv.FindFile(builder.Environment.ContentRootPath) is { } envFile)
        {
            builder.Configuration.Sources.Insert(0, new MemoryConfigurationSource
            {
                InitialData = DotEnv.Read(envFile),
            });
        }

        var options = UkweliOptions.FromConfiguration(builder.Configuration);
        builder.Services.AddSingleton(options);

        // PORT is what .env and every container platform sets. WebApplicationFactory
        // swaps in its own server, so this is inert under test.
        if (UkweliOptions.ParseInt(builder.Configuration["PORT"]) is { } port)
        {
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        }

        builder.Services.AddUkweliData(options.DatabaseUrl);
        builder.Services.AddScoped<SourceRepository>();
        builder.Services.AddScoped<AnalysisRepository>();
        builder.Services.AddScoped<AnalysisAssembler>();
        builder.Services.AddScoped<TranslationService>();
        builder.Services.AddScoped<AnalyzeService>();
        builder.Services.AddSingleton<SeedStore>(_ => new SeedStore());
        builder.Services.AddSingleton<PromptLibrary>(_ => new PromptLibrary());
        builder.Services.AddSingleton(_ => new AnthropicClient { ApiKey = options.AnthropicApiKey });
        builder.Services.AddScoped<IAiProvider, AnthropicAiProvider>();
        builder.Services.AddScoped<AnalysisPipeline>();

        builder.Services.AddScoped<CurrentUser>();
        builder.Services.AddScoped<MagicLinkService>();
        builder.Services.AddSingleton<RateLimiter>();

        // Resend in production, SMTP everywhere else.
        //
        // Keyed on the environment rather than on whether a key happens to be
        // present: a production Resend key sitting in a developer's .env would
        // otherwise silently turn local sign-in tests into real email sent to
        // real addresses. Development sends to Mailpit even with a key
        // configured.
        if (builder.Environment.IsProduction() && !string.IsNullOrWhiteSpace(options.ResendApiKey))
        {
            builder.Services.AddHttpClient<IMailer, ResendMailer>();
        }
        else
        {
            builder.Services.AddScoped<IMailer, SmtpMailer>();
        }
        builder.AddUkweliOpenApi();

        builder.Services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy =
                System.Text.Json.JsonNamingPolicy.CamelCase;
            json.SerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.Never;
        });

        return builder;
    }

    public static WebApplication MapUkweliEndpoints(this WebApplication app)
    {
        app.UseMiddleware<CurrentUserMiddleware>();

        app.MapUkweliOpenApi();
        app.MapHealthEndpoints();
        app.MapSourceEndpoints();
        app.MapAnalyzeEndpoints();
        app.MapAuthEndpoints();
        return app;
    }

    /// <summary>Builds the shared error envelope every non-2xx response uses.</summary>
    public static IResult Problem(int statusCode, string code, string message, bool retryable) =>
        Results.Json(ApiErrorResponse.Create(code, message, retryable), statusCode: statusCode);
}
