# Brand assets

`ukweli-logo.jpg` is the logo as supplied — the source of truth. The two files
served by the web app are derived from it and can be regenerated:

| File | What it is | Used for |
| --- | --- | --- |
| `apps/web/public/ukweli-icon.png` | the tile, white page margin trimmed off | favicon, apple-touch-icon, share image |
| `apps/web/public/ukweli-mark.png` | the emblem alone — head arc, Africa, tick | the in-app wordmark |

The emblem is cropped out separately because the full tile carries the word
"Ukweli" inside it. At the 36px the header draws it, that word becomes an
illegible smudge sitting next to the same word set in type.

Both are quantised to a 128-colour palette and sized to what they are actually
drawn at: the icon went from 239 KB to 17 KB, the mark from 36 KB to 3 KB. This
is not housekeeping — Ukweli is for people checking a forwarded message on a
phone, often on a slow or metered connection, and a quarter-megabyte favicon is
paid for by them.

## Colours, sampled from the artwork

| Token | Value | Where it appears |
| --- | --- | --- |
| `--brand-field` | `#002F1F` | the tile's deep green field |
| `--gold` | `#FEB31A` | the arc |
| `--brand-check` | `#067048` | the tick |
| `--cream` | `#F8F4E9` | the map and wordmark |

`--gold` is a fill colour only. As text on ivory it does not reach 4.5:1, so
`--gold-deep` (`#7A5203`) is its readable ink.
