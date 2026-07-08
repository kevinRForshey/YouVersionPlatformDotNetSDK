# YouVersion.Platform.SDK.Components

Blazor UI components for the [YouVersion Platform SDK](https://github.com/kevinRForshey/YouVersionPlatformDotNetSDK).

Provides reusable Bible reader and related Blazor components (version/book/chapter/verse pickers, `BibleReader`, `YouVersionAuth`, `VerseComponent`) built on [Microsoft Fluent UI](https://www.fluentui-blazor.net/).

`VerseComponent` renders a passage with a per-verse click-to-highlight interaction: pick a color
from its toolbar, then click a verse to highlight it in that color, on any page that displays a
passage. It loads the signed-in user's existing highlights for the displayed chapter in a single
call and renders them inline, and double-clicking a highlighted verse removes it. There is no
API for listing a user's highlights across every passage — highlights are only ever fetched
per-passage — so highlighting is only ever surfaced inline on the passage being read. Highlighting
requires a signed-in OAuth session (highlights are per-user data); when signed out, `VerseComponent`
shows a "Sign in to highlight verses" prompt instead of the color toolbar. Consumes
`IHighlightService` from `YouVersion.Platform.SDK.Services`.

`BibleReader`'s default (non-templated) passage display renders `VerseComponent` automatically, so
consumers get highlighting for free without touching `BibleReader` itself — see the `/` and
`/custom-reader` pages in `PlatformTestApp` for working examples.

## Installation

```bash
dotnet add package YouVersion.Platform.SDK.Components
```

This package transitively installs `YouVersion.Platform.SDK.Services`, `YouVersion.Platform.API`, and `YouVersion.Platform.API.Models`.

## License

MIT