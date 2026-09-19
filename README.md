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
- It does not store the raw text a user pasted — only a normalised claim.

The demo covers a small, curated set of Nigerian civic information. It is not
comprehensive or live fact-checking coverage.

## Stack

- **Backend** — C# on ASP.NET Core (.NET 10), EF Core + Postgres
- **Frontend** — React with TypeScript (Vite); a placeholder until the backend
  is complete
- **Evidence store** — a curated JSON corpus of primary official sources,
  validated before it can be seeded

The safety rules that decide every verdict live in `src/Ukweli.Evidence`, a
class library that references no HTTP or database package, so they can be tested
in isolation. The model drafts text; the server owns every safety decision.

## Development

Full setup instructions, the environment variable table, and curation
instructions land with Phase 5. Build status: Phase 0 (scaffold) in progress.

## Project documents

- [`CLAUDE.md`](CLAUDE.md) — the build specification: safety rules, data model,
  phases, and acceptance criteria.
- [`DECISIONS.md`](DECISIONS.md) — choices made where the spec was silent, and
  the one place it was overridden.
