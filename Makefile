# Ukweli — root scripts.
# `CLAUDE.md` lists these as pnpm scripts; the backend is C#, so they are make
# targets. Behaviour is unchanged.

SHELL := /bin/bash
COMPOSE_DEV := docker compose -f docker-compose.dev.yml
API := src/Ukweli.Api
DATA := src/Ukweli.Data
CLI := src/Ukweli.Cli

.DEFAULT_GOAL := help
.PHONY: help setup dev dev-stop dev-logs build test test-watch lint format \
        db-migrate db-generate db-seed verify-sources api web clean

help: ## Show available targets
	@grep -E '^[a-zA-Z0-9_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-16s\033[0m %s\n", $$1, $$2}'

setup: ## Restore .NET tools and packages, and install web dependencies
	dotnet tool restore
	dotnet restore
	cd apps/web && npm install

dev: ## Start Postgres + Mailpit, then run the API with hot reload
	$(COMPOSE_DEV) up -d
	@echo "postgres :5432   mailpit SMTP :1025   mailpit UI http://localhost:8025"
	@echo "API http://localhost:8787   Swagger http://localhost:8787/swagger"
	dotnet watch --project $(API) run

dev-stop: ## Stop the dev containers (data volume is kept)
	$(COMPOSE_DEV) down

dev-logs: ## Tail the dev container logs
	$(COMPOSE_DEV) logs -f

api: ## Run the API once, without hot reload
	dotnet run --project $(API)

web: ## Run the web placeholder dev server
	cd apps/web && npm run dev

build: ## Build the whole solution in Release
	dotnet build --configuration Release

test: ## Run every test
	dotnet test

test-watch: ## Re-run tests on change
	dotnet watch --project tests/Ukweli.Evidence.Tests test

lint: ## Verify formatting and analyzer rules without changing files
	dotnet format --verify-no-changes

format: ## Apply formatting
	dotnet format

db-migrate: ## Apply EF Core migrations to DATABASE_URL
	dotnet tool run dotnet-ef database update --project $(DATA) --startup-project $(DATA)

db-generate: ## Create a migration: make db-generate NAME=AddSources
	@test -n "$(NAME)" || { echo "usage: make db-generate NAME=MigrationName"; exit 1; }
	dotnet tool run dotnet-ef migrations add $(NAME) --project $(DATA) --startup-project $(DATA) --output-dir Migrations

db-seed: ## Load the curated store into the database (idempotent)
	dotnet run --project $(CLI) -- seed

verify-sources: ## Validate the curated store (fails while any placeholder remains)
	dotnet run --project $(CLI) -- verify-sources

clean: ## Remove build output
	dotnet clean
	rm -rf **/bin **/obj
