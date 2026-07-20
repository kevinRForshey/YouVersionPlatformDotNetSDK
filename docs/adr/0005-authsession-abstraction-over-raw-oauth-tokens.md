# 5. `AuthSession`/`IAuthSessionService` abstraction over raw OAuth tokens

## Status

Accepted

## Context

UI code (`VerseComponent`, `YouVersionAuth`) only ever needs to answer two questions: is the current
user signed in, and what's their display name? The actual answer lives behind `ITokenProvider`, which
returns an `OAuthTokenResponse` — a type from `Platform.API.OAuth` that carries raw token material
(access/refresh tokens, expiry) and is exactly the kind of type [ADR 0001](0001-layered-architecture-with-enforced-boundaries.md)
forbids `Platform.SDK.Components` from touching directly. Expiry checking (`token.IsExpired()`) and
deriving a display identity from token claims are also logic that shouldn't be duplicated in every
component that needs to know sign-in state.

## Decision

Introduce `AuthSession` (an immutable record: `IsSignedIn`, `DisplayName`, with a shared `SignedOut`
instance) and `IAuthSessionService.GetCurrentSessionAsync()` in `Platform.SDK.Services`, sitting between
UI code and `ITokenProvider`. `AuthSessionService` is the sole place that inspects the raw
`OAuthTokenResponse` — checking expiry and deriving the display identity — and translates it into the
UI-facing `AuthSession` shape. Components call `IAuthSessionService`, never `ITokenProvider`, directly.

## Consequences

- `VerseComponent` and `YouVersionAuth` depend on a two-property record instead of raw OAuth token
  internals, keeping them within the boundary `ApiClientBoundaryTests` enforces.
- Expiry semantics (what "signed in" means) live in exactly one place
  (`AuthSessionService.GetCurrentSessionAsync`), so a future change to how expiry or display-identity
  derivation works doesn't require hunting down every UI call site that reimplemented it.
- `HighlightAccessDeniedException` still exists as a separate, narrower signal for the case where
  `AuthSessionService` reports signed-in but the highlights-specific Data Exchange permission was never
  granted (sign-in alone only grants identity, not the highlights scope) — `AuthSession` intentionally
  does not try to model per-scope permission state, only overall sign-in.
