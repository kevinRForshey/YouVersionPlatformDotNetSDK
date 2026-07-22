using Platform.API.OAuth;

namespace Platform.SDK.Services
{
    /// <summary>
    /// Default <see cref="IAuthSessionService"/> implementation, backed by the configured
    /// <see cref="ITokenProvider"/>.
    /// </summary>
    public sealed class AuthSessionService(ITokenProvider tokenProvider) : IAuthSessionService
    {
        /// <inheritdoc/>
        public async Task<AuthSession> GetCurrentSessionAsync(CancellationToken cancellationToken = default)
        {
            var token = await tokenProvider.GetTokenAsync(cancellationToken);
            if (token is null || token.IsExpired())
                return AuthSession.SignedOut;

            return new AuthSession(IsSignedIn: true, DisplayName: token.GetDisplayIdentity());
        }
    }
}
