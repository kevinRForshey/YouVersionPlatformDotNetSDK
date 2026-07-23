# BiblePlatform.UsfmReferences

Part of the [Bible Platform SDK for .NET](../README.md).

Independent, unofficial C# port of [youversion/usfm-references](https://github.com/YouVersion/usfm-references)
for parsing, validating, and converting USFM (Unified Standard Format Markers) scripture references
(e.g. `"JHN.3.16"`, `"GEN.1.1-3"`, `"1SA.1.1+3"`). Versioned separately from the `Platform.*`
packages — see [Versioning & Releases](../README.md#versioning--releases) in the solution README.

This package has zero dependencies on the rest of the SDK and can be used entirely on its own to
parse or validate USFM references, unrelated to the Platform API.

## What this package provides

- `Reference` — a parsed USFM reference (book, chapter, optional section/intro, verse ranges), with
  `FromString`/`TryFromString`, `ToSingleVerses()`/`ToVerseRanges()`, and value equality.
- `VerseRange` — an inclusive `(Start, End)` verse range within a single chapter.
- `Canon` — the canonical category a book belongs to (Old Testament, New Testament, Apocrypha).
- `IUsfmReferenceService` / `UsfmReferenceService` — stateless, thread-safe high-level operations:
  book-name-to-USFM-code conversion, canon lookup, and reference/chapter/verse/passage validation.
- `BookCatalog` — the underlying USFM book code, book name, and ordinal lookup tables.

## Target framework

net10.0

## Installation

> **Not published as a package.** Clone this repo and reference the project directly — see
> [Referencing this repo locally](../README.md#referencing-this-repo-locally) in the solution
> README.

```bash
dotnet add reference ../BiblePlatformDotNetSDK/BiblePlatform.UsfmReferences/BiblePlatform.UsfmReferences.csproj
```

## Usage

```csharp
using BiblePlatform.UsfmReferences;

IUsfmReferenceService references = new UsfmReferenceService();

references.ConvertBookNameToUsfm("First Samuel"); // "1SA"
references.ConvertBookToCanon("JHN");             // Canon.NewTestament
references.IsValidVerse("JHN.3.16");              // true
references.IsValidChapter("JHN.3");               // true

Reference reference = Reference.FromString("JHN.3.16-18");
IReadOnlyList<Reference> verses = reference.ToSingleVerses(); // JHN.3.16, JHN.3.17, JHN.3.18
```

`ConvertBookNameToUsfm` is case-insensitive and ignores whitespace/punctuation, and accepts arabic,
roman, and word ordinals for numbered books — `"1 Samuel"`, `"1Sam"`, `"I Sam"`, and `"First Samuel"`
all resolve to `"1SA"`. A USFM code passed in directly (e.g. `"GEN"`) is returned unchanged.

## Related packages

`BiblePlatform.API` depends on this package for parsing scripture references such
as `Reference` and `VerseRange` used by its passage and highlight clients. See the
[solution README](../README.md) for the full package list and architecture overview.
