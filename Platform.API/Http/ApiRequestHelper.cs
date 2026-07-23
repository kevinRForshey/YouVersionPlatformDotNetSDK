using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

using Platform.API.Exceptions;

namespace Platform.API.Http;

/// <summary>
/// Shared HTTP response helpers used by all typed API clients.
/// Centralises error logging and JSON deserialization so individual clients
/// stay focused on their own request-building logic.
/// </summary>
internal static class ApiRequestHelper
{
    /// <summary>
    /// Throws <see cref="BibleApiException"/> when the response has a non-success
    /// status code, logging the failure details before throwing.
    /// </summary>
    internal static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string url,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogError(
                "API request to '{Url}' failed with HTTP {StatusCode} {ReasonPhrase}. Response body: {Body}",
                url, (int)response.StatusCode, response.ReasonPhrase, body);
            throw new BibleApiException(
                response.StatusCode,
                $"API request to '{url}' failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).",
                body);
        }
    }

    /// <summary>
    /// Issues a GET to <paramref name="relativeUrl"/>, checks for success, and
    /// deserializes the JSON body as <typeparamref name="T"/>.
    /// </summary>
    internal static async Task<T?> GetJsonAsync<T>(
        HttpClient httpClient,
        string relativeUrl,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .GetAsync(relativeUrl, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, relativeUrl, logger, cancellationToken).ConfigureAwait(false);

        return await response.Content
            .ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
