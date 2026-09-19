# CLAUDE.md — Ukweli build specification

You are building **Ukweli**, an evidence-first civic claim checker for Nigeria, due as a hackathon demo on **21 September 2026**. Build it exactly as specified here. Where this spec is silent, choose the simplest option and record the choice in `DECISIONS.md`.

**How to work:** build phase by phase, in order. Each phase is a vertical slice — schema, logic, API, and tests for one capability — and must end with all tests green and a conventional commit. **Stop at the end of every phase**, run `make test`, summarize what was built and what passed, and wait for approval before starting the next phase.

---

## 1. What Ukweli does

A resident pastes a civic claim (a forwarded message about a levy, a payment demand, a disease-outbreak rumor). The system checks it against a **small curated store of verified primary sources** and returns one verdict — `supported` | `contradicted` | `mixed_unclear` | `insufficient_evidence` — with cited source excerpts, publication/checked dates, what remains unknown, and a safe next step.

The core value is honesty: an unknown claim gets `insufficient_evidence`, never a confident answer. The AI model drafts text; **the server owns every safety decision**.

Backend first. The web app in this repo stays a placeholder until the backend is complete.

---

## 2. Non-negotiable safety rules

Implement each rule as a pure, unit-tested function in `src/Ukweli.Evidence`. These override convenience everywhere.

1. The model only ever sees the claim plus excerpts retrieved from the curated store. Never give it open web access or let it add sources.
2. Every source ID the model cites is validated against the retrieved set. Unknown ID → strip it; if the verdict loses its support, downgrade it.
3. `supported` requires ≥1 primary source with relation `supports`. `contradicted` requires ≥1 primary source with explicit contrary content. Otherwise downgrade (to `mixed_unclear` or `insufficient_evidence`).
4. Empty retrieval → `insufficient_evidence`, and the verdict model call is skipped entirely. Absence of evidence is never treated as disproof.
5. A secondary source never overrides a primary; on conflict, force `mixed_unclear`.
6. No numeric confidence scores anywhere — schema, API, or copy.
7. The `action` field is either grounded in a cited excerpt or replaced with a fixed generic caution ("Confirm directly with <issuer> before paying or sharing."). Maintain the allowlist in code.
8. AI call fails or times out (8 s hard timeout; one retry on extraction only) → HTTP 503 with a retryable error envelope. Never fabricate a result.
9. Never invent source records, URLs, or excerpts. Placeholder seed records must carry `"placeholder": true` and fail `make verify-sources` until a human replaces them.

---

## 3. Stack and monorepo layout

> **Stack amendment.** This section originally specified a TypeScript backend
> (Hono, Drizzle, Zod, pnpm workspaces, Turborepo). **The backend is built in C#
> on ASP.NET Core**, per `README.md`. Nothing else in this document changes —
> every safety rule, verdict, phase, data-model column, and "do not" still
> applies exactly as written. `DECISIONS.md` carries the full mapping from each
> Node-shaped instruction to its .NET form; where the rest of this spec says
> `packages/evidence`, read `src/Ukweli.Evidence`, and so on.

C# 14 on .NET 10, nullable reference types and warnings-as-errors everywhere.
The web placeholder stays TypeScript (Vite + React).

```
ukweli/
├─ src/
│  ├─ Ukweli.Api/          # ASP.NET Core minimal API (the product of this spec)
│  ├─ Ukweli.Evidence/     # sources.json, seeds.json, retrieval + ALL guardrails as
│  │                       # pure functions. References NO ASP.NET Core, EF Core or
│  │                       # Npgsql package — the no-HTTP/no-DB rule is enforced by
│  │                       # the compiler, not by convention.
│  ├─ Ukweli.Data/         # EF Core schema, migrations, seed + verify-sources commands
│  └─ Ukweli.Contracts/    # request/response DTOs + validation. No project references.
├─ tests/
│  ├─ Ukweli.Evidence.Tests/   # xUnit — the guardrails, in isolation
│  └─ Ukweli.Api.Tests/        # xUnit — endpoints over HTTP against a real Postgres
├─ apps/
│  └─ web/                 # Vite React TS placeholder that renders "Ukweli" and proxies /api in dev
├─ prompts/
│  ├─ extract.v1.txt
│  └─ verdict.v1.txt
├─ docker/
│  ├─ api.Dockerfile
│  └─ web.Dockerfile
├─ docker-compose.yml      # prod: postgres, api, web, caddy
├─ docker-compose.dev.yml  # dev: postgres + mailpit
├─ Ukweli.sln
├─ Directory.Build.props   # shared compiler settings for every project
├─ global.json             # pins the SDK
├─ dotnet-tools.json       # pins dotnet-ef
├─ Makefile                # the root scripts below
├─ .env.example
├─ DECISIONS.md
└─ README.md
```

Root scripts, as `make` targets: `make dev` (compose dev stack + watch), `make test`, `make build`, `make verify-sources`, `make db-migrate`, `make db-seed`.

**Environment variables** (document all in `.env.example`; secrets must never reach any client bundle):
`DATABASE_URL`, `ANTHROPIC_API_KEY`, `APP_URL`, `AUTH_ENABLED`, `BETTER_AUTH_SECRET`, `RESEND_API_KEY`, `SMTP_HOST`/`SMTP_PORT` (dev Mailpit), `RATE_LIMIT_ANALYZE_PER_MIN` (default 10).

**Error envelope** (every non-2xx response): `{ "error": { "code": string, "message": string, "retryable": boolean } }`.

---

## 4. Data model (EF Core + Postgres)

**Sources** (table `sources`)
| column | type / notes |
| --- | --- |
| id | text PK — readable slug, e.g. `ncdc-lassa-sitrep-w33-2026` |
| issuer | text |
| title | text |
| sourceType | `primary_official` \| `secondary_trusted` |
| jurisdiction | `NG` \| `NG-LA` |
| topic | `payments_levies` \| `disease_outbreaks` |
| publishedAt | date |
| checkedAt | date |
| url | text — the specific document, never an index page |
| collectionUrl | text nullable — the index it was found on |
| excerpt | text — verbatim quoted passage |
| placeholder | boolean default false |
| active | boolean default true |

**ClaimAnalyses** (table `claim_analyses`)
| column | type / notes |
| --- | --- |
| id | text PK — nanoid(10), used in `/r/{id}` share URLs |
| userId | nullable FK → auth user (Phase 4) |
| normalizedClaim | text — **store only this, never the raw pasted input** |
| jurisdiction | text nullable |
| status | enum of the four verdicts |
| explanation | text |
| simpleExplanation | text — shorter, same evidence, no new facts |
| unknowns | jsonb string[] |
| action | text |
| actionIsGeneric | boolean |
| sourceIds | jsonb string[] |
| isSeeded | boolean |
| modelVersion | text — `provider/model@prompt-vN` |
| createdAt | timestamptz |

Plus the identity tables for magic-link auth (created in Phase 4). Column names above are the C# property names; EF Core maps them to `snake_case` columns.

---

## 5. Build phases (vertical slices, backend first)

### Phase 0 — Scaffold
Monorepo skeleton, tooling, and a running dev stack.
- .NET solution + `Directory.Build.props` (nullable, warnings-as-errors); `.editorconfig`; `dotnet format`.
- `docker-compose.dev.yml` with `postgres:16-alpine` (volume) and `mailpit` (SMTP 1025, UI 8025).
- `src/Ukweli.Api`: ASP.NET Core minimal API with `GET /healthz` → `{ ok, db, modelConfigured }`.
- `apps/web`: Vite React placeholder rendering "Ukweli", dev proxy for `/api`.
- GitHub Actions CI: restore → build → test on push.
- **Done when:** `docker compose -f docker-compose.dev.yml up` boots; `curl /healthz` shows `db: true`; `dotnet test` green; CI green.

### Phase 1 — Evidence slice
The curated store, end to end: data → validation → read APIs.
- `src/Ukweli.Data`: `Source` entity + migration; seed command loading `src/Ukweli.Evidence/data/sources.json`.
- `sources.json`: two fully-structured example records, one per topic, drawn from these real collections (a human curates the specific documents):
  - Lagos payments & levies index: https://lagosstate.gov.ng/services/payments_levies (JS-rendered index — the specific notice pages/PDFs behind it are the sources)
  - NCDC disease situation reports: https://ncdc.gov.ng/diseases/sitreps (dated PDF sitreps)
  Both example records: `excerpt: "PLACEHOLDER — curator must replace with verbatim excerpt"`, `placeholder: true`.
- `make verify-sources`: schema-validate, reject duplicate ids, HTTP-check every `url`, and **fail while any `placeholder: true` remains**.
- API: `GET /api/sources/:id` (metadata + excerpt + url + `externalContentMayChange: true`; clean 404) and `GET /api/examples` (see Phase 2 seeds; may return `[]` until then).
- Tests: seed idempotency, verify:sources failure modes, 404s.
- **Done when:** seeded DB serves a source over HTTP; verify:sources fails on placeholders and passes when they're replaced in a test fixture.

### Phase 2 — Deterministic analyze slice
The full analyze → persist → share loop with **zero AI**: this slice alone must be demo-able.
- `src/Ukweli.Evidence/data/seeds.json`: three seeded claims **with complete hand-written analyses** — one `supported`, one `contradicted`, one `insufficient_evidence`.
- `src/Ukweli.Data`: `ClaimAnalysis` entity + migration.
- `POST /api/analyze` `{ text, exampleId? }`: trim; require 20–1,000 chars else 400 with a helpful message. If `exampleId` matches, or normalized text equals a seeded claim, return the stored analysis (persisted with `isSeeded: true`).
- `GET /api/results/:id`: stored analysis with resolved sources; clean 404.
- `shareText` built server-side: status word + "as checked on <date>" caveat + primary source URL + `APP_URL/r/<id>`. **Never contains the user's input.**
- Response shape: `{ id, normalizedClaim, jurisdiction, status, explanation, simpleExplanation, unknowns[], action, actionIsGeneric, sources[{ id, issuer, title, url, excerpt, publishedAt, checkedAt, relation: 'supports'|'conflicts'|'context' }], checkedOn, shareText }`.
- Tests: input edges (19/20/1000/1001 chars, blank, whitespace); all three seeded verdicts over HTTP (`WebApplicationFactory`) against a test Postgres; shareText never contains input.
- **Done when:** all three seeded claims return correct verdicts via curl; results reload by id.

### Phase 3 — AI analysis slice
Free-text claims through the model, wrapped in the guardrails.
- `prompts/extract.v1.txt` and `prompts/verdict.v1.txt`; record `modelVersion` on every analysis.
- Pipeline for non-seeded text:
  1. **extract** (Anthropic API, JSON-schema output): `{ normalizedClaim, jurisdiction|null, effectiveDate|null, affectedGroup|null, requestedAction|null }`. Location-dependent claim with null jurisdiction → scoped-limitation analysis (`insufficient_evidence`, explanation asks the user to name the place). No verdict call.
  2. **retrieve** (no AI, in `src/Ukweli.Evidence`): filter `active` sources by jurisdiction + topic keyword overlap; simple scoring with a relevance floor. Corpus ≤15 records — no embeddings, no vector store.
  3. **verdict** (JSON-schema output): claim + retrieved excerpts only → `{ status, rationale, citedSourceIds, unknowns, action, simpleExplanation }`.
  4. **validate** (pure functions): safety rules 2–7, then persist and respond.
- AI client behind an `IAiProvider` interface; tests use a stub.
- 8 s hard timeout; one retry on extract only; failure → 503 retryable. Seeded claims never touch this path.
- Tests: every guardrail rule 1–9 with the stub — unknown cited id; empty retrieval; secondary-only support; action outside allowlist; timeout → 503; downgrade paths.
- **Done when:** a free-text claim outside the corpus returns an honest `insufficient_evidence`; killing the API key leaves seeded claims working and free text returning 503.

### Phase 4 — Auth slice (magic link only)
Passwordless login gating **only** saved history. The demo must work fully with auth off.
- Magic-link sign-in only, EF Core-backed token + session store, cookie sessions. No password routes anywhere.
- Flow: user submits email → sign-in link sent → clicking it creates the session.
- Mailer behind an `IMailer` interface: `ResendMailer` (`RESEND_API_KEY`) in production; SMTP to Mailpit in dev.
- `AUTH_ENABLED=false` hides all auth routes and treats every request as anonymous.
- Public (no login): analyze, results, sources, examples, healthz. Authenticated: `GET /api/me/analyses` (caller's analyses, newest first); an authenticated analyze sets `userId` on the row.
- Per-IP rate limit on `POST /api/analyze` (`RATE_LIMIT_ANALYZE_PER_MIN`, default 10), signed in or not.
- Tests: magic-link round trip against Mailpit in dev/integration; `/api/me/analyses` 401 when anonymous; everything public still works with `AUTH_ENABLED=false`.
- **Done when:** you can request a link, click it in Mailpit, and see your past checks — and flipping `AUTH_ENABLED=false` changes nothing else.

### Phase 5 — Production packaging
- `docker/api.Dockerfile` (multi-stage `mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0`; applies EF Core migrations on boot) and `docker/web.Dockerfile` (static build).
- `docker-compose.yml`: postgres (volume), api, web, caddy (reverse proxy + automatic HTTPS; `/api/*` → api, else web).
- `README.md`: purpose and limits, architecture sketch, ≤5-command setup, curation instructions (how to replace placeholders and run `make verify-sources`), env var table.
- Finish `DECISIONS.md`.
- **Done when — overall definition of done:** fresh clone → `cp .env.example .env` → `docker compose -f docker-compose.dev.yml up` → `make db-migrate && make db-seed` → all three seeded claims return correct verdicts via curl; `make test` green; magic-link login works via Mailpit; `docker compose up` builds the production stack.

---

## 6. Do not

- Do not build the real web UI — placeholder only until the backend is approved.
- Do not add passwords, OAuth providers, or user profile fields.
- Do not store raw claim text, add analytics, or log claim text (log analysis ids and timings).
- Do not add embeddings, vector stores, queues, or caching layers — the corpus is ≤15 records.
- Do not fabricate source records, URLs, or excerpts, and do not remove the `placeholder` failure from `make verify-sources`.
- Do not skip a phase's tests or merge a phase with failing CI.
