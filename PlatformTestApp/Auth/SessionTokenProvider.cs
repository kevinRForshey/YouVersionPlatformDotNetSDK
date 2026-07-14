using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

using Platform.API.OAuth;

namespace PlatformTestApp.Auth;

/// <summary>
/// Per-user replacement for the library's default <c>InMemoryTokenProvider</c>, which is a
/// process-wide singleton and leaks one user's token to every other user. Backs the token with
/// <see cref="IDistributedCache"/>, keyed by the stable per-browser id from
/// <see cref="CircuitSessionKeyAccessor"/>, so each browser session gets its own copy and the
/// token remains readable for the full lifetime of an interactive Blazor Server circuit (not just
/// during the HTTP request that stored it).
/// </summary>
/// <remarks>
/// Must be registered <b>before</b> <c>AddYouVersionOAuth</c>, since the library only adds its
/// default via <c>TryAddSingleton</c>:
/// <code>
/// builder.Services.AddScoped&lt;CircuitSessionKeyAccessor&gt;();
/// builder.Services.AddScoped&lt;ITokenProvider, SessionTokenProvider&gt;();
/// builder.Services.AddYouVersionOAuth(o => { ... });
/// </code>
/// </remarks>
public sealed class SessionTokenProvider : ITokenProvider
{
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    private readonly CircuitSessionKeyAccessor _keyAccessor;
    private readonly IDistributedCache _cache;

    public SessionTokenProvider(CircuitSessionKeyAccessor keyAccessor, IDistributedCache cache)
    {
        _keyAccessor = keyAccessor;
        _cache = cache;
    }

    public async Task<OAuthTokenResponse?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var key = _keyAccessor.GetKey();
        if (key is null)
            return null;

        var json = await _cache.GetStringAsync(CacheKey(key), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json))
            return null;

        var stored = JsonSerializer.Deserialize<StoredToken>(json);
        if (stored is null)
            return null;

        return new OAuthTokenResponse
        {
            AccessToken = stored.AccessToken,
            RefreshToken = stored.RefreshToken,
            IdToken = stored.IdToken,
            TokenType = stored.TokenType,
            ExpiresIn = stored.ExpiresIn,
            ReceivedAt = stored.ReceivedAt,
        };
    }

    public async Task StoreTokenAsync(OAuthTokenResponse token, CancellationToken cancellationToken = default)
    {
        var key = _keyAccessor.GetKey()
            ?? throw new InvalidOperationException(
                "No session key available to store the OAuth token. StoreTokenAsync must be called " +
                "from a request with a live HttpContext (e.g. the /auth/callback-complete endpoint).");

        // OAuthTokenResponse.ReceivedAt is [JsonIgnore] -- it must be carried separately here,
        // or a reloaded token deserializes with ReceivedAt = "now" and looks freshly issued
        // even if it's about to expire.
        var stored = new StoredToken(
            token.AccessToken, token.RefreshToken, token.IdToken,
            token.TokenType, token.ExpiresIn, token.ReceivedAt);

        await _cache.SetStringAsync(CacheKey(key), JsonSerializer.Serialize(stored), CacheOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ClearTokenAsync(CancellationToken cancellationToken = default)
    {
        var key = _keyAccessor.GetKey();
        if (key is not null)
            await _cache.RemoveAsync(CacheKey(key), cancellationToken).ConfigureAwait(false);
    }

    private static string CacheKey(string sessionKey) => $"yv_oauth_token:{sessionKey}";

    private sealed record StoredToken(
        string AccessToken,
        string? RefreshToken,
        string? IdToken,
        string TokenType,
        int ExpiresIn,
        DateTimeOffset ReceivedAt);
}
