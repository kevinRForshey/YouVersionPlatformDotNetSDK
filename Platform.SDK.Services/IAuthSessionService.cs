namespace Platform.SDK.Services
{
    /// <summary>
    /// Exposes the current user's sign-in state to UI/consumer code without leaking
    /// <c>Platform.API.OAuth</c> types (<c>ITokenProvider</c>, <c>OAuthTokenResponse</c>) across
    /// the Services boundary.
    /// </summary>
    public interface IAuthSessionService
    {
        /// <summary>
        /// Returns the current sign-in state, based on whether a non-expired token is stored.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        Task<AuthSession> GetCurrentSessionAsync(CancellationToken cancellationToken = default);
    }
}
