# Ukweli API — multi-stage build.
# Stage 1 restores and publishes; stage 2 carries only the runtime and the
# published output, so the SDK, the NuGet cache and the source never ship.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copied first and restored on their own, so a source-only change reuses the
# cached restore layer instead of re-downloading every package.
# .editorconfig carries the analyzer rules, including the exemption for
# EF-generated migrations. Without it the image build fails on style rules that
# pass locally — which is exactly what happened the first time.
COPY global.json Directory.Build.props .editorconfig ./
COPY src/Ukweli.Api/Ukweli.Api.csproj        src/Ukweli.Api/
COPY src/Ukweli.Contracts/Ukweli.Contracts.csproj src/Ukweli.Contracts/
COPY src/Ukweli.Data/Ukweli.Data.csproj      src/Ukweli.Data/
COPY src/Ukweli.Evidence/Ukweli.Evidence.csproj src/Ukweli.Evidence/
COPY src/Ukweli.Cli/Ukweli.Cli.csproj        src/Ukweli.Cli/
RUN dotnet restore src/Ukweli.Api/Ukweli.Api.csproj \
 && dotnet restore src/Ukweli.Cli/Ukweli.Cli.csproj

COPY src/ src/
COPY prompts/ prompts/

RUN dotnet publish src/Ukweli.Api/Ukweli.Api.csproj -c Release -o /app/api --no-restore \
 && dotnet publish src/Ukweli.Cli/Ukweli.Cli.csproj -c Release -o /app/cli --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The runtime image is deliberately minimal and ships neither of these:
#   libgssapi-krb5-2 — Npgsql loads it for GSSAPI, and the seed command fails
#     outright without it ("libgssapi_krb5.so.2: cannot open shared object file")
#   curl — the container's own health check needs a client inside the container,
#     because the API port is exposed to the compose network but not published
#     to the host
RUN apt-get update \
 && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl \
 && rm -rf /var/lib/apt/lists/*

# The prompts are read at runtime and are part of what the model version names,
# so they ship beside the binaries rather than being baked into them.
COPY --from=build /app/api ./
COPY --from=build /app/cli ./cli/
COPY --from=build /src/prompts ./prompts/
COPY docker/api-entrypoint.sh /usr/local/bin/api-entrypoint.sh

# The .NET runtime image already ships a non-root user named `app`; creating
# another would need adduser, which this image does not carry.
RUN chmod +x /usr/local/bin/api-entrypoint.sh \
 && chown -R app /app
USER app

ENV ASPNETCORE_ENVIRONMENT=Production \
    PORT=8787
EXPOSE 8787

ENTRYPOINT ["/usr/local/bin/api-entrypoint.sh"]
