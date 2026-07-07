using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.API.Exceptions;

namespace Platform.API.OAuth;

/// <summary>
/// Default implementation of <see cref="IYouVersionOAuthClient"/>.
/// </summary>
internal sealed class YouVersionOAuthClient : IYouVersionOAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly YouVersionOAuthOptions _options;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<YouVersionOAuthClient> _logger;

    public YouVersionOAuthClient(
        HttpClient httpClient,
        IOptions<YouVersionOAuthOptions> options,
        ITokenProvider tokenProvider,
        ILogger<YouVersionOAuthClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public AuthorizationRequest BuildAuthorizationUrl(string? state = null)
    {
        if (state is not null && string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State cannot be empty or whitespace when provided.", nameof(state));

        var pkce = GeneratePkce();
        var resolvedState = state ?? Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
        var nonce = Base64UrlEncode(RandomNumberGenerator.GetBytes(24));

        var scopes = _options.Scopes ?? string.Empty;
        if (!scopes.Split(' ').Contains("openid", StringComparer.Ordinal))
            scopes = (scopes.Length > 0 ? scopes + " " : "") + "openid";

        var redirectUri = _options.RedirectUri?.AbsoluteUri.TrimEnd('/') ?? string.Empty;

        var query = new StringBuilder();
        query.Append("?response_type=code");
        query.Append("&client_id="); query.Append(Uri.EscapeDataString(_options.ClientId));
        if (redirectUri.Length > 0)
        {
            query.Append("&redirect_uri="); query.Append(Uri.EscapeDataString(redirectUri));
        }
        query.Append("&nonce="); query.Append(Uri.EscapeDataString(nonce));
        query.Append("&state="); query.Append(Uri.EscapeDataString(resolvedState));
        query.Append("&code_challenge="); query.Append(Uri.EscapeDataString(pkce.CodeChallenge));
        query.Append("&code_challenge_method=S256");
        query.Append("&scope="); query.Append(Uri.EscapeDataString(scopes));

        var url = new Uri(_options.AuthorizationEndpoint + query.ToString());

        _logger.LogDebug("Building authorization URL.");
        return new AuthorizationRequest { AuthorizationUrl = url, Pkce = pkce };
    }

    /// <inheritdoc />
    public bool ValidateState(string? expectedState, string? actualState)
    {
        if (string.IsNullOrEmpty(expectedState) || string.IsNullOrEmpty(actualState))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expectedState);
        var actualBytes = Encoding.UTF8.GetBytes(actualState);

        // Lengths must match for FixedTimeEquals; a length mismatch is not itself sensitive
        // (the state is a public URL query parameter), so an early return here is fine.
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    /// <inheritdoc />
    public async Task<OAuthTokenResponse> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Authorization code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(codeVerifier))
            throw new ArgumentException("PKCE code verifier is required.", nameof(codeVerifier));

        _logger.LogDebug("Exchanging authorization code for tokens.");

        var formData = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId,
            ["code"] = code,
            ["code_verifier"] = codeVerifier
        };
        if (_options.RedirectUri is not null)
            formData["redirect_uri"] = _options.RedirectUri.AbsoluteUri.TrimEnd('/');

        var token = await PostTokenRequestAsync(formData, cancellationToken).ConfigureAwait(false);
        await _tokenProvider.StoreTokenAsync(token, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Authorization code exchange succeeded. Token expires in {ExpiresIn}s.", token.ExpiresIn);
        return token;
    }

    /// <inheritdoc />
    public async Task<OAuthTokenResponse> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);

        if (existing?.RefreshToken is not { Length: > 0 } refreshToken)
        {
            _logger.LogWarning("Token refresh requested but no refresh token is stored.");
            throw new InvalidOperationException(
                "No refresh token is available. The user must sign in again.");
        }

        _logger.LogDebug("Refreshing access token.");

        var formData = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        };

        var token = await PostTokenRequestAsync(formData, cancellationToken).ConfigureAwait(false);
        await _tokenProvider.StoreTokenAsync(token, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Token refresh succeeded. New token expires in {ExpiresIn}s.", token.ExpiresIn);
        return token;
    }

    /// <inheritdoc />
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _tokenProvider.ClearTokenAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("User signed out; token cleared from provider.");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<OAuthTokenResponse> PostTokenRequestAsync(
        Dictionary<string, string> formData,
        CancellationToken cancellationToken)
    {
        // Use the absolute token endpoint URL directly. HttpClient.BaseAddress is not
        // configured for this client; absolute URIs bypass BaseAddress cleanly.
        using var content = new FormUrlEncodedContent(formData);
        using var response = await _httpClient
            .PostAsync(_options.TokenEndpoint.AbsoluteUri, content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("OAuth token request failed with HTTP {StatusCode} {ReasonPhrase}.", (int)response.StatusCode, response.ReasonPhrase);
            throw new YouVersionApiException(
                response.StatusCode,
                $"OAuth token request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).",
                body);
        }

        var token = await response.Content
            .ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return token ?? throw new YouVersionEmptyResponseException(
            "OAuth token endpoint returned an empty response body.");
    }

    private static PkceValues GeneratePkce()
    {
        var verifierBytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Base64UrlEncode(verifierBytes);

        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Base64UrlEncode(challengeBytes);

        return new PkceValues
        {
            CodeVerifier = verifier,
            CodeChallenge = challenge,
            CodeChallengeMethod = "S256"
        };
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
