# 3. Client-side token-bucket rate limiting via a `DelegatingHandler`

## Status

Accepted

## Context

The Platform API enforces its own server-side rate limits and will return `429` once a
client exceeds them. Relying solely on reacting to `429` responses (retry/backoff after the fact) means
every consumer app independently discovers the limit by tripping it in production, and a burst from one
part of a consumer's app (e.g. a page that fans out several `GetChaptersAsync` calls) can still exhaust
the budget before any single request comes back with a `429` to react to.

## Decision

Apply a per-client token-bucket limiter (`System.Threading.RateLimiting.TokenBucketRateLimiter`) inside
the SDK itself, as an `OutboundRateLimitingHandler : DelegatingHandler` inserted into every typed
client's `HttpClient` pipeline (`AddBibleApiClients`). Requests that can't acquire a token within
the configured queue limit fail fast with an `HttpRequestException` rather than blocking indefinitely or
silently dropping the request.

Limits (`OutboundRequestsPerSecond`, `OutboundBurstSize`, `OutboundQueueLimit`) are configuration, not
hardcoded, since they need to match whatever tier of the platform's API a given consumer is provisioned
for.

## Consequences

- Consumers get predictable local throttling behavior without needing to implement their own limiter or
  read the platform's rate-limit documentation before their first production incident.
- The local limit is independent of the server's actual limit and must be configured to a value the
  consumer believes matches it (`BibleApiOptions.OutboundRequestsPerSecond`/`OutboundBurstSize`) —
  if server-side limits change, the local values become wrong until manually updated. This is a known
  gap: there's no dynamic discovery of the server's actual limit.
- The handler's own overhead (acquiring/releasing a lease per request) is small but non-zero; it's
  tracked by `Platform.API.Benchmarks/RateLimitingHandlerBenchmarks`, which configures a burst far larger
  than any realistic call volume so the benchmark isolates handler overhead from actual throttling.
