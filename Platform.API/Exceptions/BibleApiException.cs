using System;
using System.Net;

namespace Platform.API.Exceptions;

/// <summary>
/// Exception thrown when the Platform API returns a non-success HTTP response.
/// </summary>
/// <remarks>
/// <see cref="StatusCode"/> always reflects the actual wire-level HTTP status for this type.
/// For a successful response whose body was null, empty, or unparsable, see the dedicated
/// <see cref="BibleEmptyResponseException"/> subtype instead of a fabricated status code.
/// </remarks>
public class BibleApiException : Exception
{
    /// <summary>The HTTP status code returned by the API.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The raw response body returned by the API, if available.</summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BibleApiException"/>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by the API.</param>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    public BibleApiException(
        HttpStatusCode statusCode,
        string message,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
