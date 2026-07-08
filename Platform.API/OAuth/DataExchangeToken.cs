using System.Text.Json.Serialization;

namespace Platform.API.OAuth;

/// <summary>
/// A short-lived token returned by <c>POST /data-exchange/token</c>, used to drive the user to
/// the Data Exchange approval page for one or more requested permissions (e.g. <c>highlights</c>).
/// </summary>
/// <remarks>
/// This is distinct from an OAuth access token: it only authorizes a single approval-page visit
/// and expires quickly (<see cref="ExpiresIn"/> seconds). It is not itself a bearer token for
/// calling protected resource endpoints.
/// </remarks>
public sealed record DataExchangeToken
{
    /// <summary>The short-lived token to pass to the approval page as a query parameter.</summary>
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    /// <summary>Always <c>data_exchange</c>.</summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;

    /// <summary>The token's lifetime in seconds (typically 300).</summary>
    [JsonPropertyName("expires_in")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int ExpiresIn { get; init; }
}
