using System;
using System.ComponentModel.DataAnnotations;

namespace Platform.API.OAuth;

/// <summary>
/// Configuration options for YouVersion OAuth 2.0 with PKCE.
/// Bind to the <c>YouVersionOAuth</c> configuration section or configure inline.
/// </summary>
public sealed class YouVersionOAuthOptions
{
    /// <summary>The configuration section name used when binding from <c>IConfiguration</c>.</summary>
    public const string SectionName = "YouVersionOAuth";

    /// <summary>
    /// The OAuth 2.0 client identifier registered in the YouVersion developer portal.
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The URI the authorization server redirects to after the user grants or denies access.
    /// Must match a URI registered in the YouVersion developer portal.
    /// </summary>
    public Uri? RedirectUri { get; set; }

    /// <summary>
    /// The OAuth 2.0 authorization endpoint URL.
    /// Defaults to the YouVersion authorization server.
    /// </summary>
    public Uri AuthorizationEndpoint { get; set; } =
        new("https://api.youversion.com/auth/authorize");

    /// <summary>
    /// The identity callback endpoint URL. Browser clients hitting <see cref="AuthorizationEndpoint"/>
    /// get redirected back with identity fields only (<c>yvp_id</c>, <c>user_name</c>,
    /// <c>user_email</c>, <c>profile_picture</c>, <c>state</c>) rather than a redeemable
    /// <c>code</c> — this is step 2 of YouVersion's documented sign-in flow
    /// (https://developers.youversion.com/sign-in-apis): those identity fields are sent here to
    /// obtain the actual authorization code. See
    /// <see cref="IYouVersionOAuthClient.CompleteIdentityCallbackAsync"/>.
    /// Defaults to the YouVersion callback server.
    /// </summary>
    public Uri AuthCallbackEndpoint { get; set; } =
        new("https://api.youversion.com/auth/callback");

    /// <summary>
    /// The OAuth 2.0 token endpoint URL.
    /// Defaults to the YouVersion token server.
    /// </summary>
    public Uri TokenEndpoint { get; set; } =
        new("https://api.youversion.com/auth/token");

    /// <summary>
    /// The base Data Exchange endpoint URL, used to request additional per-resource permissions
    /// (e.g. <c>highlights</c>) beyond basic sign-in. See
    /// <see cref="IYouVersionOAuthClient.RequestPermissionsAsync"/>.
    /// </summary>
    public Uri DataExchangeEndpoint { get; set; } =
        new("https://api.youversion.com/data-exchange");

    /// <summary>
    /// Space-separated OAuth scopes to request. The only scopes YouVersion's sign-in API
    /// supports are <c>openid</c>, <c>profile</c>, and <c>email</c> — there is no separate
    /// scope for passages or highlights. Sign-in alone only grants identity; resource
    /// permissions like <c>highlights</c> require the separate Data Exchange consent flow
    /// (see <see cref="IYouVersionOAuthClient.RequestPermissionsAsync"/>).
    /// </summary>
    public string Scopes { get; set; } = "openid profile email";

    /// <summary>
    /// How many seconds before a token's actual expiry to proactively refresh it inside
    /// <see cref="Platform.API.Http.OAuthBearerTokenHandler"/>.
    /// Defaults to 60 seconds.
    /// </summary>
    public int OAuthTokenExpiryBufferSeconds { get; set; } = 60;
}
