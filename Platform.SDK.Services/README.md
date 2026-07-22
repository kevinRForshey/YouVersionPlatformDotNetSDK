# YouVersion.Platform.SDK.Services.Unofficial

Part of the [YouVersion Platform SDK for .NET](../README.md).

Business-logic services for the [YouVersion Platform SDK](https://github.com/kevinRForshey/YouVersionPlatformDotNetSDK).

Provides `VersionService`, `PassageService`, `BookService`, `ChapterService`, `HighlightService`, and `BibleReaderStateService` — the stateful layer between the raw HTTP client ([`YouVersion.Platform.API.Unofficial`](../Platform.API/README.md), using types from [`YouVersion.Platform.API.Models.Unofficial`](../Platform.API.Models/README.md)) and the Blazor component library ([`YouVersion.Platform.SDK.Components.Unofficial`](../Platform.SDK.Components/README.md)).

## Installation

```bash
dotnet add package YouVersion.Platform.SDK.Services.Unofficial
```

## Fetching a passage

`IPassageService.GetPassageAsync` has two overloads: pass a pre-built `Reference` directly, or
pass raw book/chapter/verse primitives and let the service build the `Reference`/`VerseRange`
internally. Callers (including `BibleReader` and `CustomReader` in this solution) should prefer
the primitive overload so `Reference`/`VerseRange` construction lives in one place rather than
being duplicated at each call site.

```csharp
// Single verse
Passage passage = await passageService.GetPassageAsync(
    versionId: 3034, bookUsfm: "JHN", chapter: 3, verseStart: 16);

// Verse range
Passage range = await passageService.GetPassageAsync(
    versionId: 3034, bookUsfm: "JHN", chapter: 3, verseStart: 16, verseEnd: 17);
```

## Fetching chapters for a book

```csharp
IReadOnlyList<Chapter> chapters = await chapterService.GetChaptersAsync(
    versionId: 3034, bookUsfm: "GEN");
```

## Highlighting a passage

`IHighlightService` wraps the highlights API ([`Platform.API`](../Platform.API/README.md)'s `IHighlightClient`). Reads
(`GetHighlightsAsync`, `GetRecentColorsAsync`) work with the app-key auth every other service in
this package uses. Writes (`CreateOrUpdateHighlightAsync`, `ClearHighlightsAsync`) require an
OAuth bearer token from a signed-in user who has granted the `highlights` Data Exchange permission
— see `AddYouVersionOAuth` in [`YouVersion.Platform.API.Unofficial`'s README](../Platform.API/README.md#oauth-setup-optional) for that setup. There's no opaque
highlight id: a highlight is identified by the `(bibleId, passage)` pair, and colors are always raw
hex strings without a leading `#` (e.g. `"44aa44"`), not an enum — the API has no fixed palette.

```csharp
// Look up existing highlights for a chapter (one entry per highlighted verse)
IReadOnlyList<Highlight> highlights = await highlightService.GetHighlightsAsync(
    bibleId: 3034, passage: chapterReference);

// Create or update a highlight
Highlight highlight = await highlightService.CreateOrUpdateHighlightAsync(
    bibleId: 3034, passage: verseReference, color: "44aa44");

// Remove it
await highlightService.ClearHighlightsAsync(bibleId: 3034, passage: verseReference);
```

[`Platform.SDK.Components`](../Platform.SDK.Components/README.md)'s `VerseComponent` (and, through it, `BibleReader`) is the recommended way
to consume this service — it already handles sign-in checks, loading, and the color-picker UI. See
that package's README for how to enable highlighting when composing your own reader instead of
using `BibleReader` directly.

## Related packages

- [`YouVersion.Platform.API.Models.Unofficial`](../Platform.API.Models/README.md) — the model types these services operate on.
- [`YouVersion.Platform.API.Unofficial`](../Platform.API/README.md) — the raw HTTP client this package wraps.
- [`YouVersion.Platform.SDK.Components.Unofficial`](../Platform.SDK.Components/README.md) — the Blazor UI layer built on top of these services.

## License

MIT