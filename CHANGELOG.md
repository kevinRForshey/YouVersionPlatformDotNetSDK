# Changelog

All notable changes to this project are documented here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Package versions are MinVer-derived from
git tags (see `README.md` — "Versioning & Releases"); nothing has been tagged yet, so everything
below is pre-`v1.0.0`.

## [Unreleased]

### Fixed
- Cross-user OAuth token leakage: `PlatformTestApp` now registers a scoped, session-backed
  `SessionTokenProvider` before calling `AddYouVersionOAuth`, instead of relying on the library's
  default singleton `InMemoryTokenProvider` (which is documented as console/test-only).
- `IHighlightClient`'s HTTP pipeline was missing `OAuthBearerTokenHandler`, causing authenticated
  highlight requests to fail silently.
- OAuth token exchange used a relative URI against an `HttpClient` with no `BaseAddress`.
- `InMemoryTokenProvider` is now thread-safe (`SemaphoreSlim`-guarded) and `IDisposable`.
- `OAuthBearerTokenHandler` now single-flights concurrent token refreshes instead of firing a
  separate refresh call per in-flight request at token expiry.
- `Verse.cs` used the incorrect namespace `Platform.API.Models.Models`; corrected to
  `Platform.API.Models`.
- `BibleVersionSummary.Copyright` was missing `[JsonPropertyName("copyright")]` and likely never
  deserialized from real API responses.
- `YouVersion.UsfmReferences` package metadata incorrectly attributed copyright to "YouVersion";
  corrected to reflect it as an unofficial, independently-authored port.

### Changed
- `YouVersionApiException` no longer overloads `HttpStatusCode.OK`/`NotFound` to mean "empty body" —
  introduced a dedicated `YouVersionEmptyResponseException` for that case.
- `Reference`/`VerseRange` construction centralized in `PassageService` instead of being duplicated
  across `BibleReader.razor.cs`, `PassageDisplay.razor.cs`, and `CustomReader.razor`.
- The synthetic unsigned-JWT query-parameter sign-in shortcut in `PlatformTestApp` is now gated
  behind `IsDevelopment()`.

### Removed
- `Microsoft.FluentUI.AspNetCore.Components` (+ `.Emoji`/`.Icons`) dependency from
  `Platform.SDK.Components` — the SDK's Bible components never used any Fluent tags; the dependency
  moved to `PlatformTestApp`, the actual consumer of Fluent's toast/dialog/tooltip providers.
- Dead code: `OAuthCallback.razor` (superseded, unreachable OAuth callback duplicate), `PassageDisplay`
  component (broken, unreferenced), empty `BibleVerseComponent.*` scaffolding, `Areas/MyFeature`
  Razor Class Library template page, unused `VerseComponent.razor.js`, five unused `[Parameter]`s
  on `VersePicker`, and the default `Counter.razor`/`Weather.razor` scaffold pages (with their
  `NavMenu` links).
- `RichardSzalay.MockHttp` test dependency (unused) and consolidated duplicate hand-rolled
  `CapturingHandler` test fakes into one shared implementation.

### Added
- `Platform.API.Tests/Architecture/ApiClientBoundaryTests.cs` — architecture-fitness test enforcing
  that `PlatformTestApp`/`Platform.SDK.Components` never reference `Platform.API` client types
  directly.
- Integration test resolving `IHighlightClient` from DI and asserting a real `Authorization` header
  reaches the outgoing request.
- `[Required]` validation on `YouVersionOAuthOptions.ClientId`; both API-client and OAuth options
  now use `AddOptions<T>().ValidateOnStart()` so misconfiguration fails at startup, not mid-request.
