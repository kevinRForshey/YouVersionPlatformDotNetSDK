using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.API.OAuth;

namespace Platform.API.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that retrieves the current OAuth access token from
/// <see cref="ITokenProvider"/> and attaches it as an
/// <c>Authorization: Bearer &lt;token&gt;</c> header.
/// If the token is expired and a refresh token exists, it is refreshed automatically.
/// </summary>
internal sealed class OAuthBearerTokenHandler : DelegatingHandler
{
    private readonly ITokenProvider _tokenProvider;
    private readonly IBibleOAuthClient _oAuthClient;
    private readonly BibleOAuthOptions _oAuthOptions;
    private readonly ILogger<OAuthBearerTokenHandler> _logger;

    // HttpClientFactory caches and reuses a single handler-chain instance (including this
    // handler) across many concurrent requests for the handler-lifetime window (default 2
    // minutes). Without this guard, every concurrent request that observes an expired token
    // would independently call RefreshTokenAsync, racing to hit the token endpoint and
    // potentially invalidating each other's refresh token. This field single-flights those
    // concurrent refreshes into one shared call.
    private Task<OAuthTokenResponse>? _refreshTask;
    private readonly object _refreshGate = new();

    public OAuthBearerTokenHandler(
        ITokenProvider tokenProvider,
        IBibleOAuthClient oAuthClient,
        IOptions<BibleOAuthOptions> oAuthOptions,
        ILogger<OAuthBearerTokenHandler> logger)
    {
        _tokenProvider = tokenProvider;
        _oAuthClient = oAuthClient;
        _oAuthOptions = oAuthOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);

        if (token is not null && token.IsExpired(_oAuthOptions.OAuthTokenExpiryBufferSeconds))
        {
            _logger.LogDebug("OAuth access token expired; attempting transparent refresh.");
            try
            {
                token = await GetOrStartRefreshAsync().ConfigureAwait(false);
            }
            catch (System.InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Token refresh unavailable; proceeding without bearer token.");
                token = null;
            }
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        }
        else
        {
            _logger.LogDebug("No OAuth token stored; request will proceed unauthenticated.");
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the single in-flight refresh task, starting one if none is currently running.
    /// Concurrent callers that observe an expired token while a refresh is already underway
    /// await the same task instead of each issuing their own call to the token endpoint.
    /// </summary>
    /// <remarks>
    /// The shared refresh runs to completion independent of any individual request's
    /// <see cref="CancellationToken"/> — cancelling one caller's HTTP request must not abort
    /// the refresh for every other concurrent caller awaiting the same result.
    /// </remarks>
    private Task<OAuthTokenResponse> GetOrStartRefreshAsync()
    {
        lock (_refreshGate)
        {
            return _refreshTask ??= RefreshAndClearAsync();
        }
    }

    private async Task<OAuthTokenResponse> RefreshAndClearAsync()
    {
        try
        {
            return await _oAuthClient.RefreshTokenAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            lock (_refreshGate)
            {
                _refreshTask = null;
            }
        }
    }
}
