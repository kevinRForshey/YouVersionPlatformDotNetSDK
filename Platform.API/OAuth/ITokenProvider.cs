namespace Platform.API.OAuth;

/// <summary>
/// Abstraction for storing and retrieving the current OAuth token.
/// Implement this interface to control where tokens are persisted
/// </summary>
public interface ITokenProvider
{
    /// <summary>
    /// Returns the current OAuth token response, or <see langword="null"/> if no token has been stored.
    /// </summary>
    Task<OAuthTokenResponse?> GetTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the supplied <paramref name="token"/> for future retrieval.
    /// </summary>
    Task StoreTokenAsync(OAuthTokenResponse token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the currently stored token (e.g., on sign-out).
    /// </summary>
    Task ClearTokenAsync(CancellationToken cancellationToken = default);
}
