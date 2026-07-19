namespace Platform.SDK.Services
{
    /// <summary>
    /// Thrown by <see cref="IHighlightService"/> when the API rejects a highlights request as
    /// unauthorized — either the user isn't signed in, or hasn't granted the "highlights" Data
    /// Exchange permission. Lets UI/consumer code branch on this without depending on
    /// <c>Platform.API.Exceptions.YouVersionApiException</c> directly.
    /// </summary>
    public sealed class HighlightAccessDeniedException : Exception
    {
        /// <summary>Initializes a new instance of <see cref="HighlightAccessDeniedException"/>.</summary>
        /// <param name="message">A message describing why highlights access was denied.</param>
        /// <param name="innerException">The underlying exception from the API call, if any.</param>
        public HighlightAccessDeniedException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
