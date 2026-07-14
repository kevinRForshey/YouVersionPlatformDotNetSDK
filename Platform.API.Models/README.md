# YouVersion.Platform.API.Models

Domain model types for the [YouVersion Platform REST API](https://developers.youversion.com).

This package is a zero-dependency, pure-POCO library - no external NuGet dependencies required.
All types are immutable record types with System.Text.Json property-name attributes.

## Included types

| Type | Description |
|---|---|
| BibleVersionSummary | Lightweight version item returned by the list endpoint |
| BibleVersion | Full version metadata including available books |
| Book | A book within a Bible version (USFM code + human name) |
| Chapter | A chapter within a book |
| Verse | A single verse identifier |
| BibleIndex | Full book/chapter/verse structure for a version, as returned by `GET /v1/bibles/{id}/index` — the authoritative source for real, per-version counts |
| IndexBook | A book's structural position within a `BibleIndex` (title, canon, chapters, optional intro section) |
| IndexChapter | A chapter's structural position within an `IndexBook` |
| IndexVerse | A verse's structural position within an `IndexChapter` (no scripture text — use `Passage` for content) |
| IndexSection | A named, non-chapter section of a book (e.g. an introduction) |
| BookCanon | Enum identifying which scriptural canon a book belongs to (Old Testament, New Testament, Deuterocanon) |
| Passage | Scripture content returned by a passage fetch |
| PassageRequestOptions | Options controlling format, headings, and footnotes |
| PassageFormat | Text or Html enum |
| Highlight | A user Bible verse highlight — identified by (BibleId, PassageId), Color is a hex string |
| PagedResult(T) | Generic paged envelope with Data and NextPageToken |

## Target framework

net10.0

## Attribution

Always display the version Copyright field alongside Passage.Reference when showing Bible text.
