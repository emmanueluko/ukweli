# Curated evidence store

`sources.json` is the entire corpus Ukweli checks claims against. Nothing else
is consulted — the model never sees the open web.

## The two records here are not usable yet

Both are **placeholders**. They carry the real collection each source must come
from, and nothing else, because inventing a document URL, title, publication
date, or excerpt would be fabricating evidence — exactly what safety rule 9
forbids. `make verify-sources` fails while any record has `"placeholder": true`,
and that failure is the point: it is what stops an uncurated corpus reaching a
demo.

## What a curator does

For each record:

1. Open its `collectionUrl` and find the **specific document** the claim would
   be checked against — a dated notice page or PDF, never the index itself.
   - Lagos payments and levies: <https://lagosstate.gov.ng/services/payments_levies>
     (the index is JavaScript-rendered; the sources are the pages and PDFs
     behind it)
   - NCDC disease situation reports: <https://ncdc.gov.ng/diseases/sitreps>
     (dated PDF sitreps)
2. Replace `url` with that document's address.
3. Replace `title` with the document's own title, as printed on it.
4. Set `publishedAt` to the date the document itself states, and `checkedAt` to
   the date you read it. Both are `YYYY-MM-DD`.
5. Replace `excerpt` with a **verbatim** passage from the document — the exact
   words that bear on the kind of claim this source would answer. Do not
   paraphrase, summarise, or stitch together sentences from different places.
6. Change `id` to a readable slug describing the document, for example
   `ncdc-lassa-sitrep-w33-2026`.
7. Set `"placeholder": false`.

Then run:

```sh
make verify-sources
```

It validates the shape, rejects duplicate ids, checks every `url` over HTTP, and
fails while any placeholder remains.

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
