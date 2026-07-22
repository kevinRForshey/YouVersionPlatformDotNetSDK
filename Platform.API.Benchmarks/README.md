# Platform.API.Benchmarks

[BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) micro-benchmarks for the performance-
sensitive pieces of `Platform.API`: the caching decorators and the outbound rate limiter. Not
published — dev-only tooling, excluded from the solution's packable projects.

## Running

```bash
dotnet run --project Platform.API.Benchmarks -c Release
```

Pass `--filter '*BenchmarkClassName*'` to run a single suite, and see the
[BenchmarkDotNet console args docs](https://benchmarkdotnet.org/articles/guides/console-args.html)
for the rest. Always run in `Release` — BenchmarkDotNet refuses `Debug` builds.

## Suites

- **`BibleIndexMapperBenchmarks`** — cost of projecting a cached `BibleIndex` into the flattened
  `Book`/`Chapter`/`Verse` shapes, the in-memory work redone on every call even on a cache hit.
- **`CachingBibleClientBenchmarks`** — `BibleClient` (network + JSON deserialization) vs. a warm
  `CachingBibleClient` hit (HybridCache lookup + the same mapping), to quantify the caching
  decorator's actual benefit.
- **`RateLimitingHandlerBenchmarks`** — per-request overhead `OutboundRateLimitingHandler` adds
  over a no-op inner handler, with the token bucket sized so no request ever waits — isolating the
  lease acquire/release cost from any throttling.
