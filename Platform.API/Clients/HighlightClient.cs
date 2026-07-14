using System.Text.RegularExpressions;

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
/// Highlights are identified by (bible id, passage id) — the API has no opaque highlight id.
/// </summary>
/// <remarks>
/// Call <see cref="Platform.API.Extensions.ServiceCollectionExtensions.AddYouVersionOAuth"/> after
/// <c>AddYouVersionApiClients</c> to enable automatic bearer-token injection for write operations.
/// </remarks>
internal sealed partial class HighlightClient(
    HttpClient httpClient,
    ILogger<HighlightClient> logger) : IHighlightClient
{
    private const string HighlightsPath = "/v1/highlights";
    private const string RecentColorsPath = "/v1/highlights/recent-colors";

    [GeneratedRegex("^[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();

    /// <inheritdoc />
    public async Task<IReadOnlyList<Highlight>> GetHighlightsAsync(
        int bibleId,
        Reference passage,
        CancellationToken cancellationToken = default)
    {
        if (bibleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(bibleId), bibleId, "Bible id must be greater than zero.");
        ArgumentNullException.ThrowIfNull(passage);

        var passageId = ToNormalizedUsfm(passage);
        var url = $"{HighlightsPath}?bible_id={bibleId}&passage_id={Uri.EscapeDataString(passageId)}";

        logger.LogDebug("Fetching highlights for {PassageId} in Bible {BibleId}.", passageId, bibleId);

        var result = await ApiRequestHelper.GetJsonAsync<HighlightsResponse>(httpClient, url, logger, cancellationToken)
            .ConfigureAwait(false);

        var list = result?.Data ?? [];
        logger.LogDebug("Fetched {Count} highlight(s) for {PassageId}.", list.Count, passageId);
        return list;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetRecentColorsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching recently used highlight colors.");

        var result = await ApiRequestHelper.GetJsonAsync<RecentColorsResponse>(httpClient, RecentColorsPath, logger, cancellationToken)
            .ConfigureAwait(false);

        var colors = result?.Data.Select(c => c.Color).ToList() ?? [];
        logger.LogDebug("Fetched {Count} recent highlight color(s).", colors.Count);
        return colors;
    }

    /// <inheritdoc />
    public async Task<Highlight> CreateOrUpdateHighlightAsync(
        int bibleId,
        Reference passage,
        string color,
        CancellationToken cancellationToken = default)
    {
        if (bibleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(bibleId), bibleId, "Bible id must be greater than zero.");
        ArgumentNullException.ThrowIfNull(passage);
        ArgumentNullException.ThrowIfNull(color);
        if (!HexColorRegex().IsMatch(color))
            throw new ArgumentException("Color must be a 6-digit hex string without a leading '#' (e.g. \"44aa44\").", nameof(color));

        var passageId = ToNormalizedUsfm(passage);
        logger.LogDebug("Creating or updating highlight for {PassageId} in Bible {BibleId} with color {Color}.", passageId, bibleId, color);

        var payload = new CreateOrUpdateHighlightRequest
        {
            RequestId = Guid.NewGuid(),
            Highlight = new HighlightPayload
            {
                BibleId = bibleId,
                PassageId = passageId,
                Color = color
            }
        };
        using var content = JsonContent.Create(payload);
        using var response = await httpClient.PostAsync(HighlightsPath, content, cancellationToken).ConfigureAwait(false);
        await ApiRequestHelper.EnsureSuccessAsync(response, HighlightsPath, logger, cancellationToken).ConfigureAwait(false);

        var highlight = await response.Content
            .ReadFromJsonAsync<Highlight>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var result = highlight ?? throw new YouVersionEmptyResponseException(
            $"Create-or-update highlight for '{passageId}' returned an empty response body.");

        logger.LogDebug("Saved highlight for {PassageId}.", passageId);
        return result;
    }

    /// <inheritdoc />
    public async Task ClearHighlightsAsync(int bibleId, Reference passage, CancellationToken cancellationToken = default)
    {
        if (bibleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(bibleId), bibleId, "Bible id must be greater than zero.");
        ArgumentNullException.ThrowIfNull(passage);

        var passageId = ToNormalizedUsfm(passage);
        var url = $"{HighlightsPath}/{Uri.EscapeDataString(passageId)}?bible_id={bibleId}";
        logger.LogDebug("Clearing highlights for {PassageId} in Bible {BibleId}.", passageId, bibleId);

        using var response = await httpClient.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
        await ApiRequestHelper.EnsureSuccessAsync(response, url, logger, cancellationToken).ConfigureAwait(false);

        logger.LogDebug("Cleared highlights for {PassageId}.", passageId);
    }

    /// <summary>
    /// Normalizes a typed USFM reference to its string representation for API transmission.
    /// </summary>
    /// <param name="reference">The USFM reference to normalize.</param>
    /// <returns>The normalized USFM string (e.g., "JHN.3.16").</returns>
    /// <exception cref="YouVersionApiException">Thrown if the reference cannot be converted to USFM.</exception>
    private static string ToNormalizedUsfm(Reference reference)
    {
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
