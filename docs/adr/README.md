# Architecture Decision Records

Short records of decisions that shape this SDK's structure, in the lightweight
[Michael Nygard ADR format](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions): what
we decided, why, and what it costs us. These capture the *reasoning* behind choices already visible in the
code — see the top-level [README](../../README.md#architecture) and [CONTRIBUTING.md](../../CONTRIBUTING.md)
for the resulting structure itself.

New ADRs are numbered sequentially and never renumbered or deleted, even if later superseded — a superseded
record should say so and link to the one that replaces it.

| # | Title | Status |
|---|---|---|
| [0001](0001-layered-architecture-with-enforced-boundaries.md) | Layered architecture with enforced dependency boundaries | Accepted |
| [0002](0002-caching-decorator-over-hybridcache.md) | Caching as a decorator over `HybridCache`, not built into the clients | Accepted |
| [0003](0003-client-side-token-bucket-rate-limiting.md) | Client-side token-bucket rate limiting via a `DelegatingHandler` | Accepted |
| [0004](0004-unofficial-package-naming-and-minver-versioning.md) | "Unofficial" package naming and git-tag-derived versioning | Accepted |
| [0005](0005-authsession-abstraction-over-raw-oauth-tokens.md) | `AuthSession`/`IAuthSessionService` abstraction over raw OAuth tokens | Accepted |
