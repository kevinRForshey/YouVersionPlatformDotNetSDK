using Microsoft.Extensions.Caching.Distributed;

namespace PlatformTestApp.Auth;

/// <summary>
/// Tracks whether the current browser session has been granted the <c>highlights</c> Data
/// Exchange permission, so <c>Home.razor</c> can keep verse highlighting enabled across reloads
/// and revisits without re-running the approval flow. Backed by <see cref="IDistributedCache"/>,
/// keyed by the same stable per-browser id <see cref="SessionTokenProvider"/> uses via
/// <see cref="CircuitSessionKeyAccessor"/>, so each browser session gets its own copy — the same
/// cross-user isolation rationale as <see cref="SessionTokenProvider"/>, which must not be a
/// process-wide singleton.
/// </summary>
public sealed class HighlightsPermissionStore
{
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    private readonly CircuitSessionKeyAccessor _keyAccessor;
    private readonly IDistributedCache _cache;

    public HighlightsPermissionStore(CircuitSessionKeyAccessor keyAccessor, IDistributedCache cache)
    {
        _keyAccessor = keyAccessor;
        _cache = cache;
    }

    /// <summary>
    /// Returns whether <c>highlights</c> has been granted for the current browser session, or
    /// <see langword="false"/> if no session key is available yet or nothing has been stored.
    /// </summary>
    public async Task<bool> GetGrantedAsync(CancellationToken cancellationToken = default)
    {
        var key = _keyAccessor.GetKey();
        if (key is null)
            return false;

        var value = await _cache.GetStringAsync(CacheKey(key), cancellationToken).ConfigureAwait(false);
        return value == "granted";
    }

    /// <summary>
    /// Persists whether <c>highlights</c> is granted for the current browser session.
    /// </summary>
    public async Task SetGrantedAsync(bool granted, CancellationToken cancellationToken = default)
    {
        var key = _keyAccessor.GetKey();
        if (key is null)
            return;

        if (granted)
        {
            await _cache.SetStringAsync(CacheKey(key), "granted", CacheOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _cache.RemoveAsync(CacheKey(key), cancellationToken).ConfigureAwait(false);
        }
    }

    private static string CacheKey(string sessionKey) => $"yv_highlights_granted:{sessionKey}";
}
