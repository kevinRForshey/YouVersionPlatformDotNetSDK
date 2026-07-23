# 2. Caching as a decorator over `HybridCache`, not built into the clients

## Status

Accepted

## Context

Bible version/book/chapter metadata changes rarely (versions and their structure are effectively
static once published) but is fetched on nearly every page of a typical reader UI. Baking caching
directly into `BibleClient`/`PassageClient` would couple HTTP concerns to caching concerns in the same
class, make caching mandatory for every consumer (including ones with their own caching strategy, e.g.
an ASP.NET output-cache layer upstream), and complicate testing the HTTP logic in isolation.

## Decision

Keep `BibleClient` and `PassageClient` cache-unaware. Caching is a separate decorator
(`CachingBibleClient`, `CachingPassageClient`) that wraps the concrete client, backed by
`Microsoft.Extensions.Caching.Hybrid`'s `HybridCache` (in-process L1, optional Redis L2). Registration
is opt-in: `AddBibleApiClients` registers the raw clients; a separate `AddBibleCaching()` call
replaces the `IBibleClient`/`IPassageClient` DI registrations with the caching decorators, resolving the
concrete typed client directly (`sp.GetRequiredService<BibleClient>()`) to avoid a circular dependency
through the interface.

`CachingBibleClient` caches the whole per-version `BibleIndex` once and re-derives
`GetBooksAsync`/`GetChaptersAsync`/`GetVersesAsync` from the cached index in-memory via
`BibleIndexMapper`, rather than caching each of those four calls independently — so looking up a single
book or chapter never issues more than one cached fetch per version, and there's a single cache key
shape (`yv:index:{versionId}`) to reason about instead of four.

## Consequences

- Consumers who don't call `AddBibleCaching()` get direct, uncached HTTP calls with no surprise
  behavior — caching is additive, not implicit.
- Cache invalidation is TTL-only (`BibleCacheOptions`); there's no explicit invalidation API. This
  is acceptable because Bible structural/version data changes rarely, but would need revisiting if a
  future endpoint (e.g. highlights) were added to this same decorator pattern, since highlight data is
  per-user and mutates far more often.
- The in-memory `BibleIndexMapper` projection work happens on every call, cache hit or not —
  `Platform.API.Benchmarks/CachingBibleClientBenchmarks` and `BibleIndexMapperBenchmarks` exist to keep
  that cost visible as the mapping logic evolves.
