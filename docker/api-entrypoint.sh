#!/bin/sh
# Applies pending migrations, then starts the API.
#
# Migrations run here rather than at application start so a failed migration
# stops the container outright instead of leaving an API serving against a
# schema it does not match.
set -e

echo "ukweli: applying migrations"
dotnet /app/Ukweli.Api.dll --migrate-only

echo "ukweli: starting api on port ${PORT:-8787}"
exec dotnet /app/Ukweli.Api.dll
