using System.Text.Json;

using Microsoft.AspNetCore.Http;

using Platform.API.OAuth;

namespace PlatformTestApp.Auth;

/// <summary>
/// Per-user replacement for the library's default <c>InMemoryTokenProvider</c>, which is a
/// process-wide singleton and leaks one user's token to every other user. Backs the token
/// with ASP.NET Core <see cref="ISession"/> (the same session store already used for the
/// PKCE code verifier / state in <c>Program.cs</c>) so each browser session gets its own copy.
/// </summary>
/// <remarks>
/// Must be registered <b>before</b> <c>AddYouVersionOAuth</c>, since the library only adds its
/// default via <c>TryAddSingleton</c>:
/// <code>
/// builder.Services.AddScoped&lt;ITokenProvider, SessionTokenProvider&gt;();
/// builder.Services.AddYouVersionOAuth(o => { ... });
/// </code>
/// <para>
/// Known limitation: <see cref="ISession"/> requires a live <see cref="HttpContext"/>, which
/// Blazor Server only has during the initial static-SSR render of a request. Once a component
/// is running on its live interactive circuit (e.g. the second check in
/// <c>YouVersionAuth.OnAfterRenderAsync</c>), <see cref="IHttpContextAccessor.HttpContext"/> is
/// <see langword="null"/> and this provider reports "no token" rather than throwing. That's
/// harmless for the login round-trip itself (the OAuth callback redirect always starts a fresh
/// HTTP request, so the token is written and re-read with a live context), but it means a token
/// change that happens *without* a page load — e.g. a background refresh — won't be picked up by
/// an already-connected circuit without a full navigation.
/// </para>
/// </remarks>
public sealed class SessionTokenProvider : ITokenProvider
{
    private const string SessionKey = "yv_oauth_token";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public SessionTokenProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<OAuthTokenResponse?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var json = session?.GetString(SessionKey);
        if (string.IsNullOrEmpty(json))
            return Task.FromResult<OAuthTokenResponse?>(null);

        var stored = JsonSerializer.Deserialize<StoredToken>(json);
        if (stored is null)
            return Task.FromResult<OAuthTokenResponse?>(null);

        var token = new OAuthTokenResponse
        {
            AccessToken = stored.AccessToken,
            RefreshToken = stored.RefreshToken,
            IdToken = stored.IdToken,
            TokenType = stored.TokenType,
            ExpiresIn = stored.ExpiresIn,
            ReceivedAt = stored.ReceivedAt,
        };
        return Task.FromResult<OAuthTokenResponse?>(token);
    }

    public Task StoreTokenAsync(OAuthTokenResponse token, CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session
            ?? throw new InvalidOperationException(
                "No active HttpContext/Session available to store the OAuth token. " +
                "StoreTokenAsync must be called from a request with a live session " +
                "(e.g. the /auth/callback-complete endpoint), not from a connected Blazor circuit.");

        // OAuthTokenResponse.ReceivedAt is [JsonIgnore] -- it must be carried separately here,
        // or a reloaded token deserializes with ReceivedAt = "now" and looks freshly issued
        // even if it's about to expire.
        var stored = new StoredToken(
            token.AccessToken, token.RefreshToken, token.IdToken,
            token.TokenType, token.ExpiresIn, token.ReceivedAt);
        session.SetString(SessionKey, JsonSerializer.Serialize(stored));
        return Task.CompletedTask;
    }

    public Task ClearTokenAsync(CancellationToken cancellationToken = default)
    {
        _httpContextAccessor.HttpContext?.Session.Remove(SessionKey);
        return Task.CompletedTask;
    }

    private sealed record StoredToken(
        string AccessToken,
        string? RefreshToken,
        string? IdToken,
        string TokenType,
        int ExpiresIn,
        DateTimeOffset ReceivedAt);
}
