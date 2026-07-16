# YouVersion Platform SDK for .NET

[![CI](https://github.com/kevinRForshey/YouVersionPlatformDotNetSDK/actions/workflows/ci.yml/badge.svg)](https://github.com/kevinRForshey/YouVersionPlatformDotNetSDK/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

An unofficial .NET SDK for the [YouVersion Platform REST API](https://developers.youversion.com) —
typed HTTP clients, OAuth/PKCE authentication, and Blazor components for browsing Bible versions,
reading passages, and managing highlights.

> **Not affiliated with or endorsed by YouVersion / Life.Church.** This is an independent, community
> implementation built against YouVersion's public Platform API.

**See it in action:** [`PlatformTestApp`](PlatformTestApp/README.md) is a full working sample app. It
shows both the all-in-one `<BibleReader>` component and a composite reader built from the individual
pickers, so you can see exactly how to assemble your own UI — including the OAuth/PKCE sign-in flow
and click-to-highlight components.

## Packages

| Package | Description |
|---|---|
| [`Unofficial-YouVersion.Platform.API.Models`](Platform.API.Models/README.md) | Zero-dependency domain model types (Versions, Books, Chapters, Verses, Passages, Highlights). |
| [`Unofficial-YouVersion.Platform.API`](Platform.API/README.md) | Typed HTTP clients, OAuth+PKCE, caching decorators, resilience/rate-limiting pipeline, DI wiring. |
| [`Unofficial-YouVersion.Platform.SDK.Services`](Platform.SDK.Services/README.md) | Business-logic services (`VersionService`, `PassageService`, `BookService`, `HighlightService`, `BibleReaderStateService`) sitting between the raw API client and the UI layer. |
| [`Unofficial-YouVersion.Platform.SDK.Components`](Platform.SDK.Components/README.md) | Blazor components: version/book/chapter/verse pickers, `BibleReader`, click-to-highlight `VerseComponent`, `YouVersionAuth`. |
| [`Unofficial-YouVersion.UsfmReferences`](YouVersion.UsfmReferences/README.md) | Independent, unofficial C# port of [youversion/usfm-references](https://github.com/YouVersion/usfm-references) for parsing/validating USFM scripture references. Versioned separately from the `Platform.*` packages. |

See each package's own README (linked above, and published alongside it on NuGet) for installation
and usage details.

## Which package(s) do I need?

| If you want to... | Install |
|---|---|
| Call the Platform API directly (versions, passages, highlights) from a backend, console app, Azure Function, etc. — no UI | `Unofficial-YouVersion.Platform.API.Models` + `Unofficial-YouVersion.Platform.API` |
| Sign users in via OAuth/PKCE and call the API on their behalf, still with no UI | Same as above — OAuth lives in `Platform.API`. See [`Platform.API/README.md`](Platform.API/README.md) and the [OAuth/PKCE guide](docs/oauth-guide.md). |
| Build your own UI (not Blazor, or a custom Blazor UI) on top of ready-made business logic (highlight toggling, reader state, etc.) instead of raw HTTP calls | Add `Unofficial-YouVersion.Platform.SDK.Services` |
| Build a Blazor app and want ready-made UI — pickers, `BibleReader`, click-to-highlight, the `YouVersionAuth` sign-in widget | `Unofficial-YouVersion.Platform.SDK.Components` (pulls in `Services`, `API`, and `Models` transitively — just install this one) |
| Parse or validate USFM scripture references, unrelated to the Platform API | `Unofficial-YouVersion.UsfmReferences` only |

In short: install the *highest* package in the list that matches what you're building — its NuGet
dependencies pull in everything below it automatically. Only reach for `Platform.API` /
`Platform.SDK.Services` directly if you're deliberately building your own UI layer instead of using
`Platform.SDK.Components`.

## Architecture

```
Platform.API.Models        (POCO/records, zero dependencies)
        ^
        |
Platform.API                (typed HTTP clients, OAuth+PKCE, caching decorators, DI wiring)
        ^
        |
Platform.SDK.Services        (service abstractions + BibleReaderStateService)
        ^
        |
Platform.SDK.Components      (Razor components: pickers, BibleReader, YouVersionAuth)
        ^
        |
PlatformTestApp              (Blazor Server sample host, OAuth endpoints, demo pages)
```

Dependency direction is enforced automatically, not just by convention: an architecture-fitness
test (`Platform.API.Tests/Architecture/ApiClientBoundaryTests.cs`) fails the build if
`PlatformTestApp` / `Platform.SDK.Components` reference `Platform.API` client types directly instead
of going through `Platform.SDK.Services`.

`PlatformTestApp` is a working sample host demonstrating two consumption patterns — the all-in-one
`<BibleReader>` component (`/`) and manual step-by-step composition (`/custom-reader`) — plus the
full OAuth/PKCE sign-in flow.

## Quickstart

```bash
dotnet add package Unofficial-YouVersion.Platform.API.Models
dotnet add package Unofficial-YouVersion.Platform.API
```

```csharp
builder.Services.AddYouVersionApiClients(options =>
{
    options.AppKey = builder.Configuration["YouVersionApi:AppKey"]!;
});
```

For the full Blazor UI layer (pickers, reader, highlighting) add `Unofficial-YouVersion.Platform.SDK.Components`
instead — it pulls in `Services` and `API` transitively. See `Platform.API/README.md` for OAuth setup
and `Platform.SDK.Components/README.md` for the component walkthrough.

## Versioning & Releases

Package versions are derived automatically from git tags via [MinVer](https://github.com/adamralph/minver)
(`Directory.Build.props`) — there is no hand-edited version number to keep in sync across five
projects. To cut a release: tag the commit `vX.Y.Z` (e.g. `v1.0.0`) and push the tag; publishing to
NuGet.org happens via `.github/workflows/nuget-publish.yml`, triggered by publishing a GitHub Release
against that tag. Untagged commits build as `0.0.0-alpha.0.N` prereleases automatically — safe to
build and test locally or in CI, but not intended to be published.

`Unofficial-YouVersion.UsfmReferences` carries its own explicit `<Version>` (not MinVer-derived) since it
tracks the upstream Python library's release cadence independently of the `Platform.*` packages.

**Status:** no tagged release has been cut yet — every package currently builds as a `0.0.0-alpha`
prerelease. The `v1.0.0` tag is the remaining step before these packages are consumable from
NuGet.org.

## Building & testing

```bash
dotnet build
dotnet test
```

CI (`.github/workflows/ci.yml`) builds each library in dependency order, packs them to a local NuGet
feed, runs the full test suite, and builds `PlatformTestApp` against the packed output — so the
sample app exercises the same NuGet-consumer experience real consumers will get, not just project
references.

## License

MIT — see [LICENSE](LICENSE).
