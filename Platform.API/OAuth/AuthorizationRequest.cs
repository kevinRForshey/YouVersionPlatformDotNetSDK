// Ignore Spelling: Pkce

namespace Platform.API.OAuth;

/// <summary>
/// Holds the authorization URL and the matching PKCE values 
/// <see cref="IYouVersionOAuthClient.BuildAuthorizationUrl"/>.
/// </summary>
/// <remarks>
/// Store <see cref="Pkce"/>.<see cref="PkceValues.CodeVerifier"/> in server-side
/// session state and present it to <see cref="IYouVersionOAuthClient.ExchangeCodeAsync"/>
/// when the authorization server redirects the user back with a code.
/// </remarks> 
public sealed record AuthorizationRequest
{
    /// <summary>Auth url redirect fully formed</summary>
    public Uri AuthorizationUrl { get; init; } = null!;

    /// <summary>
    /// The PKCE values generated for this request
    /// </summary>
    public PkceValues Pkce { get; init; } = new();
}
