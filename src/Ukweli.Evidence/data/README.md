# Curated evidence store

`sources.json` is the entire corpus Ukweli checks claims against. Nothing else
is consulted — the model never sees the open web.

## Current state

The store holds **five curated records, all NCDC situation reports**, covering
Lassa fever, measles and diphtheria. Every excerpt was taken verbatim from the
PDF it cites.

**`payments_levies` has no sources.** The Lagos State pages named in the build
specification could not be quoted: `lagosstate.gov.ng/services/payments_levies`
renders its content in JavaScript, and `finance.lagosstate.gov.ng` and
`landsbureau.lagosstate.gov.ng` both returned HTTP 522 when the corpus was
built. Nothing was invented to fill the gap, so levy and payment claims
correctly return `insufficient_evidence` until someone curates a source.

To close that gap, open the Lagos pages in a browser, find a specific dated
notice or PDF, and add a record following the steps below.

## Adding or replacing a record

For each record:

1. Open the collection and find the **specific document** the claim would be
   checked against — a dated notice page or PDF, never the index itself.
   - Lagos payments and levies: <https://lagosstate.gov.ng/services/payments_levies>
   - NCDC disease situation reports: <https://ncdc.gov.ng/diseases/sitreps>
2. Set `url` to that document's address.
3. Set `title` to the document's own title, as printed on it. Where a listing
   and the document disagree — the NCDC listing labels one report "Week 33"
   while the document itself says "Epi Week 34" — record what the **document**
   says.
4. Set `publishedAt` to the date the document states or the date it was posted,
   and `checkedAt` to the date you read it. Both are `YYYY-MM-DD`.
5. Set `excerpt` to a **verbatim** passage — the exact words bearing on the kind
   of claim this source answers. Do not paraphrase, summarise, or stitch
   together sentences from different places. Two passages from one document
   belong in two records, each with its own id.
6. Give `id` a readable slug describing the document, e.g.
   `ncdc-lassa-sitrep-w34-2026-burden`.
7. Leave `"placeholder": false`.

Then run:

```sh
make verify-sources && make db-seed
```

Verification schema-checks the file, rejects duplicate ids, checks every URL
over HTTP, and fails while any record is still a placeholder.

## Usage restriction worth knowing

The NCDC situation reports carry this notice:

> "The report may not be used, published, or redistributed to the public."

Ukweli quotes short excerpts with attribution and links to the original. That
restriction has not been cleared with the NCDC, and should be before anything
is deployed publicly.

## Field reference

| Field | Notes |
| --- | --- |
| `id` | Readable slug, unique across the corpus. Appears in API responses. |
| `issuer` | The body that published it, as it names itself. |
| `title` | The document's own title. |
| `sourceType` | `primary_official` for a document the issuing body published itself; `secondary_trusted` for a reputable report about it. A secondary source can never on its own support or contradict a claim. |
| `jurisdiction` | `NG` nationwide, `NG-LA` for Lagos State. |
| `topic` | `payments_levies` or `disease_outbreaks`. |
| `publishedAt` | The date on the document. |
| `checkedAt` | The date a human last read it. Shown to users as the "as checked on" caveat. |
| `url` | The specific document. Never an index page. |
| `collectionUrl` | The index it was found on. Optional, but useful for re-checking. |
| `excerpt` | A verbatim passage. Never paraphrased. |
| `placeholder` | `true` until curated. Fails `verify-sources`. |
| `active` | `false` retires a record without deleting it. |

## Size

The corpus is capped at 15 records by design. Retrieval is keyword scoring over
that list — there are no embeddings and no vector store, and none are wanted.
