using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Platform.API.Configuration;
using Platform.API.Http;

namespace Platform.API.Benchmarks;

/// <summary>
/// Measures the per-request overhead <see cref="OutboundRateLimitingHandler"/> adds on top of a
/// no-op inner handler. The limiter is configured with a burst far larger than the benchmark's
/// iteration count so no request ever waits on the token bucket — this isolates the lease
/// acquire/release cost itself rather than any throttling.
/// </summary>
[MemoryDiagnoser]
public class RateLimitingHandlerBenchmarks
{
    private static readonly Uri PingUri = new("https://api.youversion.test/ping");

    private HttpClient _plainClient = null!;
    private HttpClient _rateLimitedClient = null!;
    private OutboundRateLimitingHandler _rateLimitingHandler = null!;

    [GlobalSetup]
    public void Setup()
    {
        _plainClient = new HttpClient(new NoopHandler());

        var options = Options.Create(new BibleApiOptions
        {
            AppKey = "benchmark",
            OutboundRequestsPerSecond = 1_000_000,
            OutboundBurstSize = 1_000_000,
            OutboundQueueLimit = 0
        });

        _rateLimitingHandler = new OutboundRateLimitingHandler(options, NullLogger<OutboundRateLimitingHandler>.Instance)
        {
            InnerHandler = new NoopHandler()
        };
        _rateLimitedClient = new HttpClient(_rateLimitingHandler);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _plainClient.Dispose();
        _rateLimitedClient.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task<HttpResponseMessage> Plain() => _plainClient.GetAsync(PingUri);

    [Benchmark]
    public Task<HttpResponseMessage> RateLimited() => _rateLimitedClient.GetAsync(PingUri);
}
