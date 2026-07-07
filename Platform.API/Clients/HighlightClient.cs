using Microsoft.Extensions.Logging;

using Platform.API.Exceptions;
using Platform.API.Http;
using Platform.API.Models;

using System.Net.Http.Json;
using YouVersion.UsfmReferences;

namespace Platform.API.Clients;

/// <summary>
/// HTTP implementation of <see cref="IHighlightClient"/>.
/// Read operations require only an app key; write operations require an OAuth access token
/// delivered by <see cref="Platform.API.Http.OAuthBearerTokenHandler"/>.
/// All highlight operations use typed USFM references for validation.
/// </summary>
/// <remarks>
/// Call <see cref="Platform.API.Extensions.ServiceCollectionExtensions.AddYouVersionOAuth"/> after
/// <c>AddYouVersionApiClients</c> to enable automatic bearer-token injection for write operations.
/// </remarks>
internal sealed partial class HighlightClient(
    HttpClient httpClient,
    ILogger<HighlightClient> logger,
    IUsfmReferenceService usfmReferenceService) : IHighlightClient
{
    private readonly IUsfmReferenceService _usfmReferenceService = usfmReferenceService;
    
    private const string HighlightsPath = "/v1/highlights";

    /// <inheritdoc />
    public async Task<PagedResult<Highlight>> GetHighlightsAsync(
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        if (pageToken is not null && string.IsNullOrWhiteSpace(pageToken))
            throw new ArgumentException("Page token cannot be empty or whitespace.", nameof(pageToken));

        var url = pageToken is not null
            ? $"{HighlightsPath}?page_token={System.Uri.EscapeDataString(pageToken)}"
            : HighlightsPath;

        logger.LogDebug("Fetching highlights (pageToken={PageToken}).", pageToken);

        var result = await ApiRequestHelper.GetJsonAsync<PagedResult<Highlight>>(httpClient, url, logger, cancellationToken)
            .ConfigureAwait(false);

        var list = result ?? new PagedResult<Highlight>();
        logger.LogDebug("Fetched {Count} highlight(s).", list.Data.Count);
        return list;
    }

    /// <inheritdoc />
    public async Task<Highlight> CreateHighlightAsync(
        int versionId,
        Reference usfm,
        HighlightColor color,
        CancellationToken cancellationToken = default)
    {
        if (versionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionId), versionId, "Version id must be greater than zero.");

        if (!Enum.IsDefined(color))
            throw new ArgumentOutOfRangeException(nameof(color), color, "Highlight color is invalid.");

        logger.LogDebug("Creating highlight for {Usfm} in version {VersionId} with color {Color}.", usfm, versionId, color);

        var payload = new CreateHighlightRequest
        {
            VersionId = versionId,
            Usfm = ToNormalizedUsfm(usfm),
            Color = color.ToString().ToLowerInvariant()
        };
        using var content = JsonContent.Create(payload);
        using var response = await httpClient.PostAsync(HighlightsPath, content, cancellationToken).ConfigureAwait(false);
        await ApiRequestHelper.EnsureSuccessAsync(response, HighlightsPath, logger, cancellationToken).ConfigureAwait(false);

        var highlight = await response.Content
            .ReadFromJsonAsync<Highlight>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var result = highlight ?? throw new YouVersionEmptyResponseException(
            $"Create highlight for '{ToNormalizedUsfm(usfm)}' returned an empty response body.");

        logger.LogDebug("Created highlight {HighlightId} for {Usfm}.", result.Id, ToNormalizedUsfm(usfm));
        return result;
    }

    /// <inheritdoc />
    public async Task DeleteHighlightAsync(string highlightId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(highlightId))
            throw new ArgumentException("Highlight id is required.", nameof(highlightId));

        var url = $"{HighlightsPath}/{System.Uri.EscapeDataString(highlightId)}";
        logger.LogDebug("Deleting highlight {HighlightId}.", highlightId);

        using var response = await httpClient.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
        await ApiRequestHelper.EnsureSuccessAsync(response, url, logger, cancellationToken).ConfigureAwait(false);

        logger.LogDebug("Deleted highlight {HighlightId}.", highlightId);
    }
    
    /// <summary>
    /// Normalizes a typed USFM reference to its string representation for API transmission.
    /// </summary>
    /// <param name="reference">The USFM reference to normalize.</param>
    /// <returns>The normalized USFM string (e.g., "JHN.3.16").</returns>
    /// <exception cref="YouVersionApiException">Thrown if the reference cannot be converted to USFM.</exception>
    private static string ToNormalizedUsfm(Reference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        try
        {
            return reference.ToString();
        }
        catch (Exception ex)
        {
            throw new YouVersionApiException(
                System.Net.HttpStatusCode.BadRequest,
                $"Failed to normalize USFM reference to string: {ex.Message}",
                ex.ToString());
        }
    }
}
