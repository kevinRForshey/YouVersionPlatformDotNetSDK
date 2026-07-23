> **This is a reference implementation, not a distributed SDK.** This repository demonstrates one way
> to integrate the Platform API in a .NET solution — architecture patterns, OAuth/PKCE flow,
> typed clients, Blazor components. It is not affiliated with, endorsed by, or officially supported by
> YouVersion or Life.Church, and is not published as an installable package. To use the platform,
> obtain your own App Key and review the platform's Developer Terms of Use at
> [developers.youversion.com](https://developers.youversion.com). This code is provided as-is for
> educational/reference purposes; clone and adapt it for your own project rather than treating it as a
> dependency. The included Blazor reader components (`BibleReader` and friends) are a minimal
> integration demo of the Platform API's reading/highlighting endpoints, not a Bible-reading
> application in their own right — they are not intended to replicate or compete with the
> YouVersion Bible App.

# Bible Platform SDK for .NET

[![CI](https://github.com/kevinRForshey/BiblePlatformDotNetSDK/actions/workflows/ci.yml/badge.svg)](https://github.com/kevinRForshey/BiblePlatformDotNetSDK/actions/workflows/ci.yml)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

An unofficial .NET SDK for the [Platform REST API](https://developers.youversion.com) —
typed HTTP clients, OAuth/PKCE authentication, and Blazor components for browsing Bible versions,
reading passages, and managing highlights.

> **Not affiliated with or endorsed by YouVersion / Life.Church.** This is an independent, community
> implementation built against the platform's public API.

**See it in action:** [`PlatformTestApp`](PlatformTestApp/README.md) is a full working sample app. It
shows both the all-in-one `<BibleReader>` component and a composite reader built from the individual
pickers, so you can see exactly how to assemble your own UI — including the OAuth/PKCE sign-in flow
and click-to-highlight components. If you're not using Blazor,
[`PlatformConsoleSample`](PlatformConsoleSample/README.md) is a minimal console app showing the same
API clients called directly from a plain .NET Generic Host.

## Packages

| Project | Description |
|---|---|
| [`Platform.API.Models`](Platform.API.Models/README.md) | Zero-dependency domain model types (Versions, Books, Chapters, Verses, Passages, Highlights). |
| [`Platform.API`](Platform.API/README.md) | Typed HTTP clients, OAuth+PKCE, caching decorators, resilience/rate-limiting pipeline, DI wiring. |
| [`Platform.SDK.Services`](Platform.SDK.Services/README.md) | Business-logic services (`VersionService`, `PassageService`, `BookService`, `HighlightService`, `BibleReaderStateService`) sitting between the raw API client and the UI layer. |
| [`Platform.SDK.Components`](Platform.SDK.Components/README.md) | Blazor components: version/book/chapter/verse pickers, `BibleReader`, click-to-highlight `VerseComponent`, `BibleAuth`. |
| [`BiblePlatform.UsfmReferences`](BiblePlatform.UsfmReferences/README.md) | Independent, unofficial C# port of [youversion/usfm-references](https://github.com/YouVersion/usfm-references) for parsing/validating USFM scripture references. Versioned separately from the `Platform.*` projects. |

See each project's own README (linked above) for implementation details.

## Which project(s) do I need?

| If you want to... | Reference |
|---|---|
| Call the Platform API directly (versions, passages, highlights) from a backend, console app, Azure Function, etc. — no UI | `Platform.API.Models` + `Platform.API` |
| Sign users in via OAuth/PKCE and call the API on their behalf, still with no UI | Same as above — OAuth lives in `Platform.API`. See [`Platform.API/README.md`](Platform.API/README.md) and the [OAuth/PKCE guide](docs/oauth-guide.md). |
| Build your own UI (not Blazor, or a custom Blazor UI) on top of ready-made business logic (highlight toggling, reader state, etc.) instead of raw HTTP calls | Add `Platform.SDK.Services` |
| Build a Blazor app and want ready-made UI — pickers, `BibleReader`, click-to-highlight, the `BibleAuth` sign-in widget | `Platform.SDK.Components` (references `Services`, `API`, and `Models` transitively — just reference this one) |
| Parse or validate USFM scripture references, unrelated to the Platform API | `BiblePlatform.UsfmReferences` only |

In short: reference the *highest* project in the list that matches what you're building — its
project references pull in everything below it automatically. Only reach for `Platform.API` /
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
Platform.SDK.Components      (Razor components: pickers, BibleReader, BibleAuth)
        ^
        |
PlatformTestApp              (Blazor Server sample host, OAuth endpoints, demo pages)
```

Dependency direction is enforced automatically, not just by convention: an architecture-fitness
test (`Platform.API.Tests/Architecture/ApiClientBoundaryTests.cs`) fails the build if
`PlatformTestApp` / `Platform.SDK.Components` reference `Platform.API` client types directly instead
of going through `Platform.SDK.Services`.

See [`docs/adr/`](docs/adr/README.md) for the reasoning behind this and other architectural decisions
(caching, rate limiting, package naming/versioning, the auth session abstraction).

[`PlatformTestApp`](PlatformTestApp/README.md) is a working sample host demonstrating two
consumption patterns — the all-in-one `<BibleReader>` component (`/`) and manual step-by-step
composition (`/custom-reader`) — plus the full OAuth/PKCE sign-in flow.

## Quickstart

> **This SDK is not published as an installable package.** Clone this repository and reference
> the projects directly — see [Referencing this repo locally](#referencing-this-repo-locally)
> below.

```bash
dotnet add reference ../BiblePlatformDotNetSDK/Platform.API.Models/Platform.API.Models.csproj
dotnet add reference ../BiblePlatformDotNetSDK/Platform.API/Platform.API.csproj
```

```csharp
builder.Services.AddBibleApiClients(options =>
{
    options.AppKey = builder.Configuration["BibleApi:AppKey"]!;
});
```

For the full Blazor UI layer (pickers, reader, highlighting) add `BiblePlatform.SDK.Components`
instead — it pulls in `Services` and `API` transitively. See `Platform.API/README.md` for OAuth setup
and `Platform.SDK.Components/README.md` for the component walkthrough.

## Versioning & Releases

Version numbers are derived automatically from git tags via [MinVer](https://github.com/adamralph/minver)
(`Directory.Build.props`) — there is no hand-edited version number to keep in sync across five
projects. Tag the commit `vX.Y.Z` (e.g. `v1.0.0`) locally if you want a clean version number in
build output; the tag does not need to be pushed, and no GitHub Release is created. Untagged
commits build as `0.0.0-alpha.0.N` prereleases automatically, which is fine for local use.

`BiblePlatform.UsfmReferences` carries its own explicit `<Version>` (not MinVer-derived) since it
tracks the upstream Python library's release cadence independently of the `Platform.*` projects.

**Status:** this is a clone-and-build reference implementation, not a published package — see
`CHANGELOG.md` for release history from when earlier versions were distributed via NuGet.

## Referencing this repo locally

```bash
dotnet build
dotnet test
```

Clone this repository alongside your own project, then add `ProjectReference`s to the projects you
need (see [Which project(s) do I need?](#which-projects-do-i-need) above):

```bash
dotnet add reference ../BiblePlatformDotNetSDK/Platform.API.Models/Platform.API.Models.csproj
dotnet add reference ../BiblePlatformDotNetSDK/Platform.API/Platform.API.csproj
```

CI (`.github/workflows/ci.yml`) additionally packs each library to a local NuGet feed
(`./packages`, registered as the `BibleLocal` source in `nuget.config`) as part of its own build
verification. That packing step is a CI-only build check, not a supported way to consume this SDK
— reference the projects directly as shown above.

## License

Apache License 2.0 — see [LICENSE](LICENSE).
