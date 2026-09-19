# Decisions

Choices made where `CLAUDE.md` is silent, plus the one place where it was
overridden. Newest phase last.

---

## Stack amendment — backend in C# / ASP.NET Core

**`CLAUDE.md` as delivered specified a TypeScript backend (Hono, Drizzle, Zod,
pnpm workspaces, Turborepo). The backend is instead built in C# on ASP.NET Core,
matching `README.md`'s stated plan.** This was the project owner's call, made
after Phase 0 scaffolding had begun; the Node scaffolding was removed rather
than kept alongside.

Everything in `CLAUDE.md` other than the implementation technology still stands
unchanged and unweakened — in particular all nine safety rules in section 2, the
four verdicts, the phase order, the data model, the error envelope, and the
"do not" list. What follows is how each Node-shaped instruction maps onto .NET.

| `CLAUDE.md` says | Built as | Why |
| --- | --- | --- |
| `apps/api` — Hono HTTP server | `src/Ukweli.Api` — ASP.NET Core minimal API | The one substituted technology. |
| `packages/evidence` — retrieval + guardrails as pure functions, *no HTTP or DB imports* | `src/Ukweli.Evidence` — class library that references **no** ASP.NET Core, EF Core or Npgsql package | The constraint is the point, not the language. A class library with no such references makes the rule compiler-enforced, which is stronger than convention. |
| `packages/db` — Drizzle schema, migrations, seed | `src/Ukweli.Data` — EF Core 10 + Npgsql, code-first entities, `dotnet ef` migrations, seed command | EF Core is the closest analogue: code-first models plus real, checked-in, ordered migrations. |
| `packages/shared` — Zod schemas + inferred types | `src/Ukweli.Contracts` — C# records, with FluentValidation for request rules | C# records give the types directly; validation that Zod does inline becomes explicit validators. |
| `apps/web` — Vite React TS placeholder | unchanged — `apps/web`, Vite + React + TS | Frontend was always TypeScript in both documents. |
| `pnpm test` / `pnpm build` | `dotnet test` / `dotnet build`, wrapped by `make` targets | Root scripts still exist; they drive .NET instead of Turborepo. |
| `docker/api.Dockerfile` on `node:22-alpine` | multi-stage `mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0` | Same shape, .NET base images. |
| Anthropic TypeScript SDK | `Anthropic.SDK` (or raw `HttpClient` against the Messages API) behind an `IAiProvider` interface | The spec's requirement is the interface and the stub, both of which survive. |

**`pnpm verify:sources` becomes `dotnet run --project src/Ukweli.Data -- verify-sources`,** still exposed as `make verify-sources`. Its behaviour is unchanged, including
the non-negotiable part: it fails while any record carries `placeholder: true`.

---

## Phase 0 — Scaffold

**.NET 10.0.400, targeting `net10.0`.** Three SDKs are installed locally
(8.0.424, 9.0.317, 10.0.400); `global.json` pins 10.0.400 with `latestPatch`
roll-forward so the local build, CI, and the Docker image agree. `CLAUDE.md`
pinned Node 22 for the same reason; this is the .NET equivalent.

**`dotnet-ef` is a local tool, not a global one.** It is committed in
`dotnet-tools.json` and restored with `dotnet tool restore`, so migrations run
identically on a fresh clone and in CI without anyone installing anything
globally.

**`DATABASE_URL` keeps its URL form and is translated in code.** `CLAUDE.md`
names the variable and `.env.example` documents it, but Npgsql expects
`Host=…;Port=…;Database=…;Username=…;Password=…`, not `postgres://…`. Rather
than change the documented variable, `Ukweli.Data` parses the URL form into an
Npgsql connection string and also accepts a native Npgsql string, so either
works.

**`Ukweli.Contracts` has no project references.** It holds only DTOs and the
error envelope, so both the API and the evidence library can depend on it
without either depending on the other.

**Application processes run on the host in dev, not in compose.**
`docker-compose.dev.yml` provides Postgres and Mailpit only. Bind-mount hot
reload is slower and flakier on macOS than `dotnet watch` and Vite run directly,
and the production `docker-compose.yml` in Phase 5 is what proves the containers
actually build.

**Dev database credentials are `ukweli:ukweli`.** They appear only in
`docker-compose.dev.yml` and `.env.example`, both committed, and the container
binds to localhost. Nothing real is protected by them. `.env` itself is
gitignored and holds the generated `BETTER_AUTH_SECRET`.

**API on port 8787; web dev server on 5173 proxying `/api` to it.** Keeps the
frontend origin stable for Phase 4 cookie sessions.

**`/healthz` runs a real `SELECT 1`.** A cached flag would report a healthy API
against a dead database, which is exactly the failure the check exists to catch.
`modelConfigured` reports only whether a key is present — it never calls the
model and never echoes the key.
