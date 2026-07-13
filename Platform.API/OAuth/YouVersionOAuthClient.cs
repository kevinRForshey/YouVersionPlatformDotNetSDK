using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.API.Configuration;
using Platform.API.Exceptions;

namespace Platform.API.OAuth;

/// <summary>
/// Default implementation of <see cref="IYouVersionOAuthClient"/>.
/// </summary>
internal sealed class YouVersionOAuthClient : IYouVersionOAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly YouVersionOAuthOptions _options;
    private readonly YouVersionApiOptions _apiOptions;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<YouVersionOAuthClient> _logger;

    public YouVersionOAuthClient(
        HttpClient httpClient,
        IOptions<YouVersionOAuthOptions> options,
        IOptions<YouVersionApiOptions> apiOptions,
        ITokenProvider tokenProvider,
        ILogger<YouVersionOAuthClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _apiOptions = apiOptions.Value;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public AuthorizationRequest BuildAuthorizationUrl(string? state = null, IEnumerable<string>? requestedPermissions = null)
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

        // Requesting permissions here (rather than via the separate RequestPermissionsAsync +
        // BuildDataExchangeApprovalUrl round trip) shows the consent UI as part of this same
        // sign-in redirect. In practice the grant result has been observed arriving both ways:
        // as `granted_permissions` alongside `code`/`state` on this same callback, and as a
        // separate follow-up callback carrying `data_exchange_status` (the same shape
        // ParseDataExchangeCallback handles). Callers should be prepared for either.
        if (requestedPermissions is not null)
        {
            foreach (var permission in requestedPermissions)
            {
                if (string.IsNullOrWhiteSpace(permission))
                    continue;
                query.Append("&requested_permissions="); query.Append(Uri.EscapeDataString(permission));
            }
        }

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
    public async Task<OAuthTokenResponse> CompleteIdentityCallbackAsync(
        string state,
        string yvpId,
        string? userName,
        string? userEmail,
        string? profilePicture,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State is required.", nameof(state));
        if (string.IsNullOrWhiteSpace(yvpId))
            throw new ArgumentException("yvp_id is required.", nameof(yvpId));
        if (string.IsNullOrWhiteSpace(codeVerifier))
            throw new ArgumentException("PKCE code verifier is required.", nameof(codeVerifier));

        _logger.LogDebug("Exchanging identity callback for an authorization code.");

        var query = new StringBuilder();
        query.Append("?state=").Append(Uri.EscapeDataString(state));
        query.Append("&yvp_id=").Append(Uri.EscapeDataString(yvpId));
        if (!string.IsNullOrWhiteSpace(userName))
            query.Append("&user_name=").Append(Uri.EscapeDataString(userName));
        if (!string.IsNullOrWhiteSpace(userEmail))
            query.Append("&user_email=").Append(Uri.EscapeDataString(userEmail));
        if (!string.IsNullOrWhiteSpace(profilePicture))
            query.Append("&profile_picture=").Append(Uri.EscapeDataString(profilePicture));

        var url = _options.AuthCallbackEndpoint + query.ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // No status-code check beyond "did we get a Location to follow" — YouVersion's docs say
        // this hop is a redirect (a 302 in practice), but the exact status isn't load-bearing here;
        // what matters is whether a `code` shows up in the Location's query string.
        if (response.Headers.Location is null)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "Identity callback exchange failed with HTTP {StatusCode} {ReasonPhrase} and no Location header. Response body: {Body}",
                (int)response.StatusCode, response.ReasonPhrase, body);
            throw new YouVersionApiException(
                response.StatusCode,
                $"Identity callback exchange failed with status {(int)response.StatusCode} ({response.ReasonPhrase}) and no redirect to follow.",
                body);
        }

        var location = response.Headers.Location.IsAbsoluteUri
            ? response.Headers.Location
            : new Uri(response.RequestMessage!.RequestUri!, response.Headers.Location);

        var code = FirstOrDefault(ParseQueryString(location.Query), "code");
        if (string.IsNullOrWhiteSpace(code))
            throw new YouVersionEmptyResponseException(
                "The auth callback endpoint's redirect did not include an authorization code.");

        _logger.LogDebug("Identity callback exchange succeeded; redeeming the authorization code.");
        return await ExchangeCodeAsync(code, codeVerifier, cancellationToken).ConfigureAwait(false);
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

    /// <inheritdoc />
    public async Task<DataExchangeToken> RequestPermissionsAsync(
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var permissionList = permissions as IReadOnlyList<string> ?? new List<string>(permissions);
        if (permissionList.Count == 0)
            throw new ArgumentException("At least one permission must be requested.", nameof(permissions));

        var token = await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is null)
            throw new InvalidOperationException(
                "No access token is stored. The user must sign in before requesting additional permissions.");

        _logger.LogDebug("Requesting data exchange token for permissions: {Permissions}.", string.Join(", ", permissionList));

        var appKey = RequireAppKey();
        var url = $"{_options.DataExchangeEndpoint}/token?x-yvp-app-key={Uri.EscapeDataString(appKey)}";
        using var content = JsonContent.Create(new DataExchangeRequest { RequestedPermissions = permissionList });
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "Data exchange token request failed with HTTP {StatusCode} {ReasonPhrase}. Response body: {Body}",
                (int)response.StatusCode, response.ReasonPhrase, body);
            throw new YouVersionApiException(
                response.StatusCode,
                $"Data exchange token request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).",
                body);
        }

        var result = await response.Content
            .ReadFromJsonAsync<DataExchangeToken>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result ?? throw new YouVersionEmptyResponseException(
            "Data exchange token endpoint returned an empty response body.");
    }

    /// <inheritdoc />
    public Uri BuildDataExchangeApprovalUrl(string dataExchangeToken)
    {
        if (string.IsNullOrWhiteSpace(dataExchangeToken))
            throw new ArgumentException("Data exchange token is required.", nameof(dataExchangeToken));

        var appKey = RequireAppKey();

        return new Uri(
            $"{_options.DataExchangeEndpoint}?token={Uri.EscapeDataString(dataExchangeToken)}" +
            $"&x-yvp-app-key={Uri.EscapeDataString(appKey)}");
    }

    /// <inheritdoc />
    public Uri BuildDirectDataExchangeUrl(IEnumerable<string> permissions, string? state = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var permissionList = permissions as IReadOnlyList<string> ?? new List<string>(permissions);
        if (permissionList.Count == 0)
            throw new ArgumentException("At least one permission must be requested.", nameof(permissions));

        var appKey = RequireAppKey();
        var query = new StringBuilder();
        query.Append("?x-yvp-app-key=").Append(Uri.EscapeDataString(appKey));

        foreach (var permission in permissionList)
        {
            if (string.IsNullOrWhiteSpace(permission))
                continue;
            query.Append("&requested_permissions=").Append(Uri.EscapeDataString(permission));
        }

        if (!string.IsNullOrWhiteSpace(state))
            query.Append("&state=").Append(Uri.EscapeDataString(state));

        _logger.LogDebug("Building direct data exchange URL.");
        return new Uri(_options.DataExchangeEndpoint + query.ToString());
    }

    /// <inheritdoc />
    public DataExchangeCallbackResult ParseDataExchangeCallback(Uri callbackUrl)
    {
        ArgumentNullException.ThrowIfNull(callbackUrl);
        return ParseCallbackQuery(callbackUrl.Query);
    }

    /// <inheritdoc />
    public async Task<DataExchangeCallbackResult> CompleteDataExchangeApprovalAsync(
        string dataExchangeToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dataExchangeToken))
            throw new ArgumentException("Data exchange token is required.", nameof(dataExchangeToken));

        var appKey = RequireAppKey();

        _logger.LogDebug("Completing data exchange approval for token.");

        // POST /data-exchange takes no request body — the permissions being granted were already
        // fixed when the token was created via RequestPermissionsAsync (POST /data-exchange/token).
        // The `token` query parameter alone is what the API needs to finalize the grant; sending an
        // `Authorization` bearer header here is a *separate*, mutually-exclusive path the API
        // reserves for clients that skip token creation entirely, which this SDK doesn't do.
        var url = $"{_options.DataExchangeEndpoint}?token={Uri.EscapeDataString(dataExchangeToken)}" +
            $"&x-yvp-app-key={Uri.EscapeDataString(appKey)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.SeeOther || response.Headers.Location is null)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "Data exchange completion failed with HTTP {StatusCode} {ReasonPhrase}. Response body: {Body}",
                (int)response.StatusCode, response.ReasonPhrase, body);
            throw new YouVersionApiException(
                response.StatusCode,
                $"Data exchange completion failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).",
                body);
        }

        var location = response.Headers.Location.IsAbsoluteUri
            ? response.Headers.Location
            : new Uri(response.RequestMessage!.RequestUri!, response.Headers.Location);

        var result = ParseDataExchangeCallback(location);
        _logger.LogInformation("Data exchange approval completed with status {Status}.", result.Status);
        return result;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private string RequireAppKey()
    {
        if (string.IsNullOrWhiteSpace(_apiOptions.AppKey))
            throw new InvalidOperationException(
                $"{nameof(YouVersionApiOptions)}.{nameof(YouVersionApiOptions.AppKey)} is not configured. " +
                "The Data Exchange approval page is a direct browser redirect and cannot rely on the " +
                "X-YVP-App-Key header, so the app key must be configured to include it as a query parameter.");

        return _apiOptions.AppKey;
    }

    private static DataExchangeCallbackResult ParseCallbackQuery(string query)
    {
        var parameters = ParseQueryString(query);

        var status = parameters.TryGetValue("data_exchange_status", out var statusValues) && statusValues.Count > 0
            ? statusValues[0] switch
            {
                "granted" => DataExchangeStatus.Granted,
                "cancelled" => DataExchangeStatus.Cancelled,
                "error" => DataExchangeStatus.Error,
                _ => DataExchangeStatus.Unknown
            }
            : DataExchangeStatus.Unknown;

        return new DataExchangeCallbackResult
        {
            Status = status,
            GrantedPermissions = ExtractPermissions(parameters, "granted_permissions"),
            DeniedPermissions = ExtractPermissions(parameters, "denied_permissions"),
            Error = FirstOrDefault(parameters, "error"),
            ErrorDescription = FirstOrDefault(parameters, "error_description"),
        };
    }

    private static IReadOnlyList<string> ExtractPermissions(Dictionary<string, List<string>> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var values))
            return [];

        return values
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
    }

    private static string? FirstOrDefault(Dictionary<string, List<string>> parameters, string key)
        => parameters.TryGetValue(key, out var values) ? values.FirstOrDefault() : null;

    private static Dictionary<string, List<string>> ParseQueryString(string query)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var trimmed = query.TrimStart('?');
        if (trimmed.Length == 0)
            return result;

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            var key = separatorIndex >= 0 ? pair[..separatorIndex] : pair;
            var value = separatorIndex >= 0 ? pair[(separatorIndex + 1)..] : string.Empty;

            key = Uri.UnescapeDataString(key.Replace("+", " "));
            value = Uri.UnescapeDataString(value.Replace("+", " "));

            if (!result.TryGetValue(key, out var list))
            {
                list = [];
                result[key] = list;
            }
            list.Add(value);
        }

        return result;
    }

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
