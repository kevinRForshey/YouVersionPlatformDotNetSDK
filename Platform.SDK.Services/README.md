# YouVersion.Platform.SDK.Services

Business-logic services for the [YouVersion Platform SDK](https://github.com/kevinRForshey/YouVersionPlatformDotNetSDK).

Provides `VersionService`, `PassageService`, `BookService`, `ChapterService`, and `BibleReaderStateService` — the stateful layer between the raw HTTP client (`YouVersion.Platform.API`) and the Blazor component library (`YouVersion.Platform.SDK.Components`).

## Installation

```bash
dotnet add package YouVersion.Platform.SDK.Services
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

## License

MIT