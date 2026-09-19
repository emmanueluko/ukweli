# Prompts

Versioned prompt templates for the AI analysis slice (Phase 3).

- `extract.v1.txt` — normalises a pasted claim into
  `{ normalizedClaim, jurisdiction, effectiveDate, affectedGroup, requestedAction }`.
- `verdict.v1.txt` — given the claim and **only** the excerpts retrieved from
  the curated store, drafts
  `{ status, rationale, citedSourceIds, unknowns, action, simpleExplanation }`.

Two rules govern everything in this directory:

1. The model never receives anything beyond the claim and the retrieved
   excerpts. It has no web access and cannot add sources.
2. Nothing the model returns is trusted. Every verdict passes through the pure
   validation functions in `src/Ukweli.Evidence` before it is persisted or
   returned, and those functions — not the prompt — decide the final status.

A prompt is never edited in place once it has produced stored analyses. Add
`*.v2.txt` instead, so the `modelVersion` recorded on each row
(`provider/model@prompt-vN`) keeps pointing at the text that actually ran.
