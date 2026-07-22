# 1. Layered architecture with enforced dependency boundaries

## Status

Accepted

## Context

The SDK ships five packages that build on each other: `Platform.API.Models` (POCOs), `Platform.API`
(HTTP clients, OAuth, caching, rate limiting), `Platform.SDK.Services` (business-logic services),
`Platform.SDK.Components` (Blazor UI), and the `PlatformTestApp` sample host. Consumers who only need
raw API access shouldn't have to pull in Blazor, and UI code shouldn't be able to reach past the
service layer straight into `Platform.API`'s client/OAuth internals — doing so would let a UI change
accidentally couple to HTTP/OAuth implementation details that are free to change independently.

A layering rule enforced only by code review or a README diagram erodes over time: a rushed PR adds
one `using Platform.API.Clients;` in a component, it works, it ships, and the boundary is gone with no
build failure to catch it.

## Decision

Enforce the dependency direction `Models -> API -> Services -> Components -> PlatformTestApp` with
architecture fitness tests (`Platform.API.Tests/Architecture/ApiClientBoundaryTests.cs` and
`ProjectDependencyDirectionTests.cs`) that fail CI, not just review, when violated.

`ApiClientBoundaryTests` reflects over `Platform.API`'s compiled assembly to discover every public
client/OAuth/exception type, then scans `Platform.SDK.Components` and `PlatformTestApp` source for
`using` directives or bare type references to any of them. Discovering the forbidden type list via
reflection (rather than hardcoding it) means a new public client type is covered automatically instead
of silently bypassing the test.

A small, explicit set of composition-root files (`PlatformTestApp/Program.cs`,
`Auth/OAuthCallbackHandlers.cs`, `Auth/SessionTokenProvider.cs`) are exempted — they perform the actual
OAuth/PKCE handshake and implement `ITokenProvider`, which are legitimate SDK extension points, not
boundary violations.

## Consequences

- A PR that reaches across the boundary fails CI with a specific, actionable assertion message instead
  of an easy-to-miss review comment.
- The exemption list must be kept up to date by hand as new composition-root files are added; forgetting
  to exempt a legitimate extension point produces a false-positive test failure (safe direction to fail
  in, but still friction).
- Reflection-based discovery means the test's coverage grows automatically as `Platform.API`'s public
  surface grows, at the cost of the test needing the assembly to already be built.
