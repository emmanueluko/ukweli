# Ukweli

Evidence-backed civic information verification for African communities. Check
claims, understand sources, and take informed action.

Ukweli is a hackathon proof of concept. A resident pastes a civic claim — a
forwarded message about a levy, a payment demand, a disease-outbreak rumour —
and Ukweli checks it against a small, curated store of verified primary sources.

## What it returns

One verdict, never a confidence score:

| Verdict | Meaning |
| --- | --- |
| `supported` | At least one primary official source supports the claim. |
| `contradicted` | At least one primary official source explicitly contradicts it. |
| `mixed_unclear` | Sources conflict, or only secondary sources speak to it. |
| `insufficient_evidence` | The curated store has nothing relevant to say. |

Alongside the verdict: the cited source excerpts with their URLs and
publication dates, a plain-language explanation, what remains unknown, and one
safe next step.

## What it deliberately does not do

- It does not search the open web. The model sees only the claim and excerpts
  retrieved from the curated store.
- It does not guess. An unknown claim returns `insufficient_evidence` rather
  than a confident answer, and absence of evidence is never reported as
  disproof.
- It does not produce confidence percentages.
- It does not store the raw text a user pasted — only a normalised claim — and
  it never writes claim text to a log.
- It never tells anyone to pay, to refuse to pay, or to disregard medical
  advice. Any suggested step that is not grounded in a cited excerpt is
  replaced with a fixed caution to verify with the issuing authority.

## Current coverage, stated plainly

The store holds **five curated sources, all from the NCDC**, covering Lassa
fever, measles and diphtheria. **`payments_levies` has no sources yet** — the
Lagos State pages are JavaScript-rendered or were unreachable when the corpus
was built, so no excerpt could be quoted from them. Until someone curates one,
every levy or payment claim correctly returns `insufficient_evidence`.

## Architecture

```
browser ──► Caddy (TLS, one origin)
              ├─ /api/*  ──► Ukweli.Api ──► Postgres
              └─ /*      ──► static web bundle
```

| Project | Holds |
| --- | --- |
| `src/Ukweli.Api` | ASP.NET Core minimal API, the Anthropic provider, auth |
| `src/Ukweli.Evidence` | The curated store, retrieval, and **every safety rule** |
| `src/Ukweli.Data` | EF Core entities, migrations, repositories |
| `src/Ukweli.Contracts` | Request/response DTOs and the shared error envelope |
| `src/Ukweli.Cli` | `seed` and `verify-sources` |
| `apps/web` | Vite + React placeholder |

`Ukweli.Evidence` references no ASP.NET Core, EF Core or Npgsql package, so the
rule that the safety logic stays testable without a server or database is
enforced by the compiler rather than by convention. A test asserts it against
the built assembly.

The model drafts text; the server decides. Everything the model returns passes
through pure validation before it is stored or shown.

## Setup

Requires .NET 10, Docker and Node 22.

```sh
cp .env.example .env                              # add ANTHROPIC_API_KEY if you have one
make setup                                        # restore tools, packages, web deps
docker compose -f docker-compose.dev.yml up -d    # Postgres + Mailpit
make db-migrate && make db-seed                   # schema and curated sources
make dev                                          # API on :8787, Swagger on :8787/swagger
```

`make` on its own lists every target. `make test` runs the suite — it needs the
dev Postgres, which the compose file provides along with the `ukweli_test`
database the integration tests use.

### Trying it

```sh
curl -s localhost:8787/api/examples
curl -s -X POST localhost:8787/api/analyze \
  -H 'Content-Type: application/json' \
  -d '{"exampleId":"seed-lassa-herbal-cure"}'
```

Swagger UI is at <http://localhost:8787/swagger> in development.

## Curating the evidence store

`src/Ukweli.Evidence/data/sources.json` is the whole corpus, capped at fifteen
records. `make verify-sources` schema-checks it, rejects duplicate ids, HTTP-checks
every URL, and **fails while any record is marked `"placeholder": true`** —
deliberately, so uncurated evidence cannot reach a demo.

To add a source: open its collection, find the **specific document** (never an
index page), and record its URL, title, publication date, the date you checked
it, and a **verbatim** excerpt. Do not paraphrase or stitch sentences together.
Full instructions and a field reference are in
[`src/Ukweli.Evidence/data/README.md`](src/Ukweli.Evidence/data/README.md).

Then run `make verify-sources && make db-seed`.

> **Before deploying publicly:** the NCDC situation reports carry a notice
> stating the report "may not be used, published, or redistributed to the
> public." Ukweli quotes short excerpts with attribution and links to the
> original, but that restriction has not been cleared with the NCDC.

## Environment variables

Documented in [`.env.example`](.env.example); `.env` is gitignored.

| Variable | Required | Purpose |
| --- | --- | --- |
| `DATABASE_URL` | yes | Postgres, as `postgres://…` or an Npgsql string. |
| `ANTHROPIC_API_KEY` | no | Enables free-text claims. Without it, seeded claims still work and free text returns a retryable 503. |
| `APP_URL` | yes | Public base URL, used to build `/r/{id}` share links. |
| `AUTH_ENABLED` | no | `false` by default. Hides every auth route; the demo works fully without it. |
| `BETTER_AUTH_SECRET` | if auth on | Signs sign-in tokens and session cookies. `openssl rand -base64 32`. |
| `RESEND_API_KEY` | no | Production mail. Unset means SMTP to Mailpit. |
| `SMTP_HOST` / `SMTP_PORT` | no | Dev mail, defaults to Mailpit on `localhost:1025`. |
| `RATE_LIMIT_ANALYZE_PER_MIN` | no | Per-IP limit on `/api/analyze`, default 10. |
| `PORT` | no | API port, default 8787. |

Production also reads `POSTGRES_PASSWORD` and `UKWELI_DOMAIN`.

## Production

```sh
docker compose up -d --build
```

Postgres, the API, the static web bundle, and Caddy terminating TLS. Migrations
are applied by the API container's entrypoint before the server starts, so a
failed migration stops the container rather than leaving it serving against a
schema it does not match.

## Project documents

- [`CLAUDE.md`](CLAUDE.md) — the build specification: safety rules, data model,
  phases, and acceptance criteria.
- [`DECISIONS.md`](DECISIONS.md) — choices made where the spec was silent, the
  one place it was overridden, and the known gaps.
