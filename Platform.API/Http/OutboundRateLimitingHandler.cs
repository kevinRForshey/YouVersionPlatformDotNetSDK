using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.API.Configuration;

namespace Platform.API.Http;

/// <summary>
/// Applies a per-client token-bucket rate limiter to outbound YouVersion API requests.
/// </summary>
internal sealed class OutboundRateLimitingHandler : DelegatingHandler, IDisposable
{
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger<OutboundRateLimitingHandler> _logger;

    public OutboundRateLimitingHandler(
        IOptions<YouVersionApiOptions> options,
        ILogger<OutboundRateLimitingHandler> logger)
    {
        _logger = logger;
        var value = options.Value;

        if (value.OutboundRequestsPerSecond <= 0)
            throw new InvalidOperationException($"{nameof(YouVersionApiOptions)}.{nameof(YouVersionApiOptions.OutboundRequestsPerSecond)} must be greater than zero.");

        if (value.OutboundBurstSize < value.OutboundRequestsPerSecond)
            throw new InvalidOperationException($"{nameof(YouVersionApiOptions)}.{nameof(YouVersionApiOptions.OutboundBurstSize)} must be greater than or equal to {nameof(YouVersionApiOptions.OutboundRequestsPerSecond)}.");

        if (value.OutboundQueueLimit < 0)
            throw new InvalidOperationException($"{nameof(YouVersionApiOptions)}.{nameof(YouVersionApiOptions.OutboundQueueLimit)} cannot be negative.");

        _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = value.OutboundBurstSize,
            TokensPerPeriod = value.OutboundRequestsPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = value.OutboundQueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            _logger.LogWarning("Outbound request rate-limited locally: {Method} {RequestUri}", request.Method, request.RequestUri);
            throw new HttpRequestException("Outbound request rate-limited locally by the SDK token bucket.");
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _rateLimiter.Dispose();

        base.Dispose(disposing);
    }
}


