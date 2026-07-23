# BiblePlatform.SDK.Components

Part of the [Bible Platform SDK for .NET](../README.md).

Blazor UI components for the [Bible Platform SDK](https://github.com/kevinRForshey/BiblePlatformDotNetSDK).

Provides reusable Bible reader and related Blazor components (version/book/chapter/verse pickers, `BibleReader`, `BibleAuth`, `VerseComponent`) built on plain Bootstrap markup — no UI framework dependency beyond what your host app already brings in.

> These are minimal integration demos of the Platform API's reading/highlighting endpoints —
> building blocks for embedding scripture access in your own app, not a standalone Bible-reading
> application. Not intended to replicate or compete with the YouVersion Bible App.

## Installation

> **Not published as a package.** Clone this repo and reference the project directly — see
> [Referencing this repo locally](../README.md#referencing-this-repo-locally) in the solution
> README.

```bash
dotnet add reference ../BiblePlatformDotNetSDK/Platform.SDK.Components/Platform.SDK.Components.csproj
```

This project transitively references [`Platform.SDK.Services`](../Platform.SDK.Services/README.md), [`Platform.API`](../Platform.API/README.md), and [`Platform.API.Models`](../Platform.API.Models/README.md).

Register the underlying services once at startup:

```csharp
builder.Services.AddBibleApiClients(options => { /* ... */ });
builder.Services.AddBibleOAuth(options => { /* ... */ }); // only if you need sign-in / highlighting
builder.Services.AddBibleComponents();
```

See [`Platform.API`'s README](../Platform.API/README.md#oauth-setup-optional) for `AddBibleOAuth` setup and
[`Platform.SDK.Services`'s README](../Platform.SDK.Services/README.md) for the services `AddBibleComponents()` registers.

`AddBibleComponents()` registers `IVersionService`, `IBookService`, `IChapterService`,
`IPassageService`, `IHighlightService`, and `IBibleReaderStateService` as **scoped** — one instance
per Blazor circuit/user. Every component below resolves these via `@inject`; none of them accept
data as component parameters, so nothing renders correctly outside a DI container that has run
`AddBibleComponents()`.

## Quick start

The fastest way to a working reader is the all-in-one `BibleReader`:

```razor
<BibleReader Title="Read the Bible"
             LanguageRange="en"
             OAuthError="@OAuthError" />
```

That's it — `BibleReader` composes every picker below, plus sign-in and highlighting, into one
component. Reach for the individual pickers only when you need a different layout or workflow than
`BibleReader` provides (see `/custom-reader` in `PlatformTestApp` for a working example, including
highlighting — see [Enabling highlighting in a custom composition](#enabling-highlighting-in-a-custom-composition)
below).

## Component reference

### `BibleReader`

*Namespace:* `Platform.SDK.Components.BibleComponents`
*Services used:* `IBibleReaderStateService`, `IPassageService`

The all-in-one reading experience: version/book/chapter/verse pickers, a **Read** button, sign-in
via the embedded `BibleAuth`, and the loaded passage rendered through `VerseComponent` (with
click-to-highlight) by default. This is the component most consumers should start with.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Title` | `string` | `"Bible Reader"` | Heading displayed at the top of the component. |
| `LanguageRange` | `string` | `"en"` | BCP-47 language range used to filter the version list. Cascades to `VersionPicker`. |
| `Format` | `PassageFormat` | `PassageFormat.Html` | Passage format requested from the API. Use `PassageFormat.Text` for plain text. |
| `PassageTemplate` | `RenderFragment<Passage>?` | `null` | Custom rendering for the loaded passage, receiving the `Passage` as render-fragment context. When omitted, the built-in `VerseComponent` is used. |
| `EnableHighlighting` | `bool` | `true` | Whether the default (non-templated) passage display shows the highlighting toolbar. No effect when `PassageTemplate` is supplied. |
| `LoginPath` | `string` | `"/auth/login"` | Sign-in route forwarded to the embedded `BibleAuth`. |
| `LogoutPath` | `string` | `"/auth/logout"` | Sign-out route forwarded to the embedded `BibleAuth`. |
| `OAuthError` | `string?` | `null` | OAuth error message forwarded to the embedded `BibleAuth` (e.g. from a `?oauth_error=` query parameter on the host page). |

| Event | Type | Fires when |
|---|---|---|
| `OnPassageLoaded` | `EventCallback<Passage>` | A passage finishes loading successfully. |
| `OnSignInRequested` | `EventCallback` | The user clicks "Sign in". If no delegate is provided, falls back to navigating to `LoginPath`. |
| `OnSignOutRequested` | `EventCallback` | The user clicks "Sign out". If no delegate is provided, falls back to navigating to `LogoutPath`. |
| `OnHighlightCreated` | `EventCallback<Highlight>` | A highlight is created/updated from the default passage display. No effect with a custom `PassageTemplate`. |
| `OnHighlightCleared` | `EventCallback<Highlight>` | A highlight is removed from the default passage display. No effect with a custom `PassageTemplate`. |

```razor
<BibleReader Title="Read the Bible"
             Format="PassageFormat.Html"
             OnPassageLoaded="@(p => _lastReference = p.Reference)" />
```

Custom rendering — take over how the passage is displayed while still getting the picker toolbar
and Read button for free:

```razor
<BibleReader>
    <PassageTemplate Context="passage">
        <article class="my-passage">@((MarkupString)passage.Content)</article>
    </PassageTemplate>
</BibleReader>
```

The **Read** button is disabled until a version, book, chapter, and starting verse are all
selected. Requesting a new passage cancels any in-flight request for the previous one.

---

### `VersionPicker`

*Namespace:* `Platform.SDK.Components.BibleComponents`
*Services used:* `IVersionService`, `IBibleReaderStateService`

A `<select>` of available Bible versions for a given language. Selecting a version calls
`State.SelectVersion(...)`, which resets the downstream book/chapter/verse selection.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `LanguageRange` | `string` | `"en"` | BCP-47 language range used to filter the version list. Re-fetches automatically when this parameter changes. |

```razor
<VersionPicker LanguageRange="es" />
```

Standalone, this only needs `IBibleReaderStateService` to be shared with whatever downstream
components (or your own code) react to the selection — it doesn't require `BibleReader`.

---

### `BookPicker`

*Namespace:* `Platform.SDK.Components.BibleComponents`
*Services used:* `IBibleReaderStateService`, `IBookService`

A `<select>` of books for the currently selected version (via `IBibleReaderStateService`). Shows
"Select a version first" until `State.SelectedVersion` is set, then loads and caches the book list
for that version. Selecting a book calls `State.SelectBook(...)`, resetting chapter/verse.

No parameters — entirely state-driven. Place it after a `VersionPicker` sharing the same
`IBibleReaderStateService` scope.

```razor
<VersionPicker />
<BookPicker />
```

---

### `ChapterPicker`

*Namespace:* `Platform.SDK.Components.BibleComponents`
*Services used:* `IBibleReaderStateService`

A `<select>` of chapter numbers `1..ChapterCount` for the currently selected book. Shows "Select a
book first" until `State.SelectedBook` is set. Selecting a chapter calls
`State.SelectChapter(...)`.

No parameters — entirely state-driven.

```razor
<BookPicker />
<ChapterPicker />
```

---

### `VersePicker`

*Namespace:* `Platform.SDK.Components.BibleComponents`
*Services used:* `IBibleReaderStateService`, `IChapterService`

A "From" / "To" verse-range input for the currently selected chapter. Shows "Select a chapter
first" until `State.SelectedChapter` is set. On mount (and whenever the chapter changes), it
defaults the range to the full chapter by fetching the real per-chapter verse count from
`IChapterService`; if that lookup fails, it falls back to a conservative maximum (176 — Psalm 119,
the longest chapter in the Bible) so the input still works, with a warning shown inline. Both
inputs validate against the resolved max verse and commit the range via
`State.SelectVerseRange(...)` on every valid change. The "✕" button clears the end verse (single
verse mode).

No parameters — entirely state-driven.

```razor
<ChapterPicker />
<VersePicker />
```

---

### `VerseComponent`

*Namespace:* `Platform.SDK.Components.BibleComponents.Verses`
*Services used:* `IHighlightService`, `ITokenProvider`

Renders a loaded `Passage`'s HTML content split into individually-addressable verse segments, with
optional click-to-highlight. This is what `BibleReader` renders by default — use it directly when
you're fetching passages yourself (e.g. via `IPassageService`) but still want highlighting.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Passage` | `Passage` | *(required)* | The passage to render. Marked `[EditorRequired]`. |
| `Copyright` | `string?` | `null` | Copyright notice shown in the footer. Always display the version's `Copyright` alongside the passage per the platform's attribution requirement. |
| `VersionId` | `int` | `0` | The Bible version id the passage was read from. Required to create/look up highlights. When `0` (default), the highlighting toolbar is hidden and the passage renders read-only regardless of `EnableHighlighting`. |
| `EnableHighlighting` | `bool` | `true` | Whether the highlighting toolbar and verse click/double-click interactions are enabled. When `false`, no sign-in check or highlight API calls are made at all. |

| Event | Type | Fires when |
|---|---|---|
| `OnHighlightCreated` | `EventCallback<Highlight>` | The user clicks an armed verse to create/update a highlight. |
| `OnHighlightCleared` | `EventCallback<Highlight>` | The user double-clicks a highlighted verse to remove it. |

```razor
<VerseComponent Passage="@passage"
                Copyright="@version.Copyright"
                VersionId="@version.Id" />
```

Behavior notes:
- Pick a color from the toolbar to "arm" it, then click a verse to apply that color. Double-click a
  highlighted verse to remove it.
- Highlighting requires a signed-in OAuth session — highlights are per-user data. When signed out,
  the toolbar is replaced with a "Sign in to highlight verses" prompt; the passage itself always
  renders regardless of sign-in state.
- Existing highlights for the passage's chapter are loaded in a single call and rendered inline.
  There's no API for listing a user's highlights across every passage, so highlights are only ever
  surfaced on the specific passage being read.
- A 401 from the highlights API (token expired server-side, or the user never granted the separate
  highlights permission during sign-in) surfaces as an inline prompt to sign in again, distinct from
  other load/save failures.

#### Enabling highlighting in a custom composition

`VerseComponent` supports highlighting out of the box (that's exactly what `BibleReader` uses it
for internally), so wiring it into your own hand-built reader is just a matter of deciding *when*
`EnableHighlighting` should be `true`. That decision has two parts, both driven by state that lives
outside `VerseComponent` itself:

1. **Is the user signed in?** Check via `ITokenProvider` — highlighting always requires an OAuth
   session, and `VerseComponent` won't call the highlights API at all when `EnableHighlighting` is
   `false`.
2. **Has the user granted the separate `highlights` Data Exchange permission?** Signing in does
   *not* implicitly grant it — it's requested via a separate redirect
   (`AddBibleOAuth`'s Data Exchange flow), and the result comes back on the query string (e.g.
   `?highlights=granted` / `?highlights=denied`) after the approval round-trip. Persist that grant
   somewhere durable (a cookie, distributed cache, or your own user record) so highlighting stays on
   across page reloads instead of resetting to off every time the query parameter is absent.

`PlatformTestApp`'s `Home.razor` and `/custom-reader`'s `CustomReader.razor` both follow this exact
pattern — a `HighlightsPermissionStore` (a small `IDistributedCache`-backed helper local to the test
app, not part of this package) is checked in `OnInitializedAsync`, then re-checked in
`OnAfterRenderAsync(firstRender: true)` because a token or grant persisted during the OAuth callback
round-trip may not be visible in the SSR prerender scope `OnInitializedAsync` ran in:

```razor
@inject ITokenProvider TokenProvider
@inject NavigationManager Nav

@if (_isSignedIn && !_highlightsEnabled)
{
    <button @onclick="RequestHighlightsAsync">Grant highlights access</button>
}
<VerseComponent Passage="@passage"
                VersionId="@version.Id"
                EnableHighlighting="@_highlightsEnabled" />

@code {
    [SupplyParameterFromQuery(Name = "highlights")]
    public string? HighlightsStatus { get; set; }

    private bool _highlightsEnabled;
    private bool _isSignedIn;

    protected override async Task OnInitializedAsync()
    {
        _highlightsEnabled = HighlightsStatus switch
        {
            "granted" => true,
            "denied" => false,
            _ => await PermissionStore.GetGrantedAsync() // your own persisted grant lookup
        };
        _isSignedIn = (await TokenProvider.GetTokenAsync()) is { } t && !t.IsExpired();
    }

    // Forcing a full page load bypasses Blazor's enhanced navigation, which would otherwise try to
    // diff the redirect response into the page instead of following it to the off-site approval page.
    private void RequestHighlightsAsync() => Nav.NavigateTo("/auth/request-highlights", forceLoad: true);
}
```

If you use `BibleReader` instead of composing `VerseComponent` directly, the same two checks apply —
just compute `_highlightsEnabled` the same way and pass it as `BibleReader`'s `EnableHighlighting`
parameter (this is exactly what `Home.razor` does).

---

### `BibleAuth`

*Namespace:* `Platform.SDK.Components.Auth`
*Services used:* `ITokenProvider`

A self-contained sign-in/sign-out widget. Reads the current token from `ITokenProvider` and renders
either a "Sign in" button or the signed-in user's display name plus a "Sign out" button. Embedded
inside `BibleReader`, but usable standalone anywhere you want auth controls (e.g. a page header).

| Parameter | Type | Default | Description |
|---|---|---|---|
| `LoginPath` | `string` | `"/auth/login"` | Sign-in route navigated to when `OnSignInRequested` has no delegate. |
| `LogoutPath` | `string` | `"/auth/logout"` | Sign-out route navigated to when `OnSignOutRequested` has no delegate. |
| `OAuthError` | `string?` | `null` | Error message to surface below the controls (e.g. forwarded from a `?oauth_error=` query parameter). Renders as a Bootstrap danger alert when non-null. |
| `ButtonCssClass` | `string` | `"btn btn-sm btn-outline-primary"` | CSS class applied to the sign-in/sign-out buttons. |
| `UsernameCssClass` | `string` | `"me-2"` | CSS class applied to the username `<span>` when signed in. |

| Event | Type | Fires when |
|---|---|---|
| `OnSignInRequested` | `EventCallback` | The user clicks "Sign in". If no delegate is provided, falls back to a full-page navigation to `LoginPath`. |
| `OnSignOutRequested` | `EventCallback` | The user clicks "Sign out". If no delegate is provided, falls back to a full-page navigation to `LogoutPath`. |

```razor
<BibleAuth LoginPath="/auth/login" LogoutPath="/auth/logout" />
```

Sign-in state is re-checked after the component's first interactive render in addition to
initialization — a token stored during the OAuth callback round-trip may not be visible in the SSR
prerender scope the component first ran in, so this avoids a stale "signed out" flash.

## Related packages

- [`BiblePlatform.API.Models`](../Platform.API.Models/README.md) — the model types (`Passage`, `Highlight`, etc.) these components render.
- [`BiblePlatform.API`](../Platform.API/README.md) — the raw HTTP client and OAuth setup underneath this package.
- [`BiblePlatform.SDK.Services`](../Platform.SDK.Services/README.md) — the service layer these components inject and consume.

## License

Apache License 2.0 — see [LICENSE](../LICENSE).
