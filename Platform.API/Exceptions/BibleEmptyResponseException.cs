using System.Net;

namespace Platform.API.Exceptions;

/// <summary>
/// Exception thrown when the Platform API returns a successful HTTP response
/// whose body is null, empty, or fails to deserialize into the expected type.
/// </summary>
/// <remarks>
/// This is distinct from <see cref="BibleApiException"/>'s general-purpose usage: the
/// underlying HTTP call itself succeeded, so <see cref="BibleApiException.StatusCode"/>
/// does not carry a real wire-level error status here. Callers that branch on
/// <see cref="BibleApiException.StatusCode"/> should check for this type first (or
/// catch it separately) rather than treating it as an ordinary API error.
/// </remarks>
public sealed class BibleEmptyResponseException : BibleApiException
{
    /// <summary>
    /// Initializes a new instance of <see cref="BibleEmptyResponseException"/>.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    public BibleEmptyResponseException(string message, string? responseBody = null)
        : base(HttpStatusCode.OK, message, responseBody)
    {
    }
}
