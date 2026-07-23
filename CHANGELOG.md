# Changelog

All notable changes to this project are documented here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Package versions are MinVer-derived from
git tags (see `README.md` — "Versioning & Releases").

## [Unreleased]

### Changed
- Relicensed from MIT to Apache License 2.0 (all five packages, including
  `BiblePlatform.UsfmReferences`). See `LICENSE` and the new `NOTICE` file.
- Repositioned this repository as a clone-and-build reference implementation rather than an
  installable SDK, and removed remaining "YouVersion"/"Life.Church" branding from identifiers,
  doc comments, and messages (factual/technical references — API URLs, header names, JSON
  property names, the required non-affiliation disclaimer — are unaffected).

### Fixed
- `Platform.API/README.md`'s install snippet showed a stale `Version="1.0.0"` that doesn't exist on
  the feed (actual published version was `0.1.0`), which would cause a restore failure for anyone
  copying it as-is.
- The "Additional docs" links (Getting started / Authentication / OAuth guide) on
  `Platform.API/README.md` were relative markdown links that resolve fine on GitHub but render as
  dead links on NuGet.org's README renderer. Converted to absolute GitHub URLs; same fix applied to
  the top-level `README.md`'s OAuth/PKCE guide link. `docs/getting-started.md` and
  `docs/authentication.md` were also missing entirely (the "Getting started" and "Authentication"
  links pointed at files that had never been created) — both added.

## [0.1.0] - 2026-07-14

### Added
- `IYouVersionOAuthClient.BuildAuthorizationUrl` now accepts an optional `requestedPermissions`
  parameter, appended to `/auth/authorize` as repeated `requested_permissions=...` query
  parameters. This shows the Data Exchange consent UI as part of the sign-in redirect itself,
  rather than requiring a separate `RequestPermissionsAsync` + `BuildDataExchangeApprovalUrl`
  round trip after sign-in completes; the result comes back as `granted_permissions` on the same
  callback that carries `code`/`state`. This is the recommended way to request a resource
  permission (e.g. `highlights`) at sign-in time — `RequestPermissionsAsync`/
  `BuildDataExchangeApprovalUrl` remain for requesting a permission later, without a full
  sign-in round trip.
- `IYouVersionOAuthClient.ParseDataExchangeCallback` — parses the `data_exchange_status`,
  `granted_permissions`, `denied_permissions`, `error`, and `error_description` query parameters
  YouVersion appends to the app's callback URL after the Data Exchange approval page, returning a
  typed `DataExchangeCallbackResult`/`DataExchangeStatus`.
- `IYouVersionOAuthClient.CompleteDataExchangeApprovalAsync(dataExchangeToken)` — completes a Data
  Exchange approval via `POST /data-exchange?token={token}` using a previously-issued
  `RequestPermissionsAsync` token, for confidential clients that can skip the browser-rendered
  approval page. Requires the OAuth `HttpClient` to have `AllowAutoRedirect` disabled so the `303`
  response's `Location` header can be read directly.
- Data Exchange documentation section in `Platform.API/README.md` covering the full
  request-token → approval-page → callback flow.

### Fixed
- The Data Exchange approval page never appeared during `PlatformTestApp`'s sign-in flow.
  Compounding causes: (1) `PlatformTestApp` was missing `requested_permissions` on
  `/auth/authorize` entirely, which is what actually triggers the consent UI during sign-in (see
  the `Added` entry for `BuildAuthorizationUrl`'s new `requestedPermissions` parameter);
  (2) `Results.Redirect` was called with `Uri.ToString()` instead of `Uri.AbsoluteUri` on the
  authorization/approval URLs in `PlatformTestApp/Program.cs` — `.ToString()` un-escapes
  percent-encoded characters for display (e.g. turning `scope=openid%20profile%20email` back into
  a literal, RFC-invalid `scope=openid profile email` with raw spaces), silently corrupting the
  outbound redirect. Note: in live testing, the completed consent grant has been observed coming
  back both as `granted_permissions` on the same callback as `code` *and* as a separate
  `data_exchange_status` callback (the same shape `ParseDataExchangeCallback`/
  `BuildDataExchangeApprovalUrl` use) — `PlatformTestApp` handles both; don't remove either path
  without confirming via a real completed sign-in that it's unreachable.
- `BuildDataExchangeApprovalUrl` was missing the app key entirely. The Data Exchange approval
  page is a top-level browser redirect and can't carry the `X-YVP-App-Key` header, so the app key
  must be sent as an `x-yvp-app-key` query parameter instead — the URL would otherwise fail to
  resolve the calling app. `YouVersionOAuthClient` now reads `YouVersionApiOptions.AppKey` for
  this (and throws `InvalidOperationException` if it isn't configured).
- `RequestPermissionsAsync` (`POST /data-exchange/token`) wasn't sending `x-yvp-app-key` either,
  even though the API reference lists it as an accepted query parameter for resolving the calling
  app on that endpoint; it's now included alongside the existing bearer token.
- `CompleteDataExchangeApprovalAsync` sent a `requested_permissions` JSON body and an
  `Authorization` bearer header to `POST /data-exchange` — neither matches the documented
  contract. That endpoint takes **no request body**; the permissions granted are whatever was
  fixed when the Data Exchange token was created via `RequestPermissionsAsync`. The method now
  takes that token directly (`CompleteDataExchangeApprovalAsync(dataExchangeToken)`) and sends it
  as the `token` query parameter, matching `POST /data-exchange?token={token}`. **Breaking change**
  to the (unreleased) method signature.
- `PlatformTestApp`'s `/auth/callback-complete` swallowed any failure from
  `RequestPermissionsAsync`/`BuildDataExchangeApprovalUrl` and silently redirected to the same URL
  as a successful sign-in (`/?auth_mode=code`), so a broken Data Exchange request looked identical
  to the user never being asked for `highlights` at all. The error now reaches the `oauth_error`
  query parameter the home page already renders.
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
