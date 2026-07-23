using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.API.Clients;
using Platform.API.Configuration;
using Platform.API.Models;

namespace Platform.API.Benchmarks;

/// <summary>
/// Compares fetching a Bible index directly against <see cref="BibleClient"/> (network + JSON
/// deserialization on every call) with a warm <see cref="CachingBibleClient"/> hit (HybridCache
/// lookup + the same in-memory mapping), to quantify the caching decorator's actual benefit.
/// </summary>
[MemoryDiagnoser]
public class CachingBibleClientBenchmarks
{
    private const int VersionId = 1;

    private ServiceProvider _provider = null!;
    private BibleClient _rawClient = null!;
    private CachingBibleClient _cachingClient = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var json = SampleBibleIndex.CreateJson();
        var httpClient = new HttpClient(new StaticJsonHandler(json))
        {
            BaseAddress = new Uri("https://api.youversion.test")
        };
        _rawClient = new BibleClient(httpClient, NullLogger<BibleClient>.Instance);

        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddHybridCache();
        _provider = services.BuildServiceProvider();

        var cache = _provider.GetRequiredService<HybridCache>();
        _cachingClient = new CachingBibleClient(_rawClient, cache, new BibleCacheOptions());

        // Warm the cache so the benchmarked CachedHit() call is always a hit, not a miss.
        await _cachingClient.GetIndexAsync(VersionId);
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();

    [Benchmark(Baseline = true)]
    public Task<BibleIndex> Uncached() => _rawClient.GetIndexAsync(VersionId);

    [Benchmark]
    public Task<BibleIndex> CachedHit() => _cachingClient.GetIndexAsync(VersionId);
}
