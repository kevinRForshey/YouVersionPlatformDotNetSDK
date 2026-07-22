namespace Platform.SDK.Services
{
    /// <summary>
    /// The current user's sign-in state, as seen by UI/consumer code through
    /// <see cref="IAuthSessionService"/> instead of the underlying OAuth token directly.
    /// </summary>
    /// <param name="IsSignedIn">Whether a non-expired token is currently stored.</param>
    /// <param name="DisplayName">The signed-in user's display identity, if available.</param>
    public sealed record AuthSession(bool IsSignedIn, string? DisplayName)
    {
        /// <summary>Shared instance representing "no signed-in user".</summary>
        public static readonly AuthSession SignedOut = new(false, null);
    }
}
