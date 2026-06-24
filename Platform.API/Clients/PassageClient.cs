using System.Text;
using Microsoft.Extensions.Logging;
using Platform.API.Exceptions;
using Platform.API.Http;
using Platform.API.Models;
using YouVersion.UsfmReferences;

namespace Platform.API.Clients;

/// <summary>
/// Default implementation of <see cref="IPassageClient"/> backed by the YouVersion Platform REST API.
/// Requires all USFM references to be provided as typed <see cref="Reference"/> objects for validation.
/// </summary>
internal sealed partial class PassageClient(
    HttpClient httpClient,
    ILogger<PassageClient> logger,
    IUsfmReferenceService usfmReferenceService) : IPassageClient
{
    private readonly IUsfmReferenceService _usfmReferenceService = usfmReferenceService;
    /// <inheritdoc />
    public async Task<Passage> GetPassageAsync(
        int versionId,
        Reference usfm,
        PassageRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (versionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionId), versionId, "Version id must be greater than zero.");

        var resolvedOptions = options ?? PassageRequestOptions.Default;
        var normalizedUsfm = usfm.ToString();
        var url = BuildPassageUrl(versionId, normalizedUsfm, resolvedOptions);

        logger.LogDebug("Fetching passage {Usfm} from version {VersionId} (format={Format}).", 
            normalizedUsfm, versionId, resolvedOptions.Format);

        var passage = await ApiRequestHelper.GetJsonAsync<Passage>(httpClient, url, logger, cancellationToken)
            .ConfigureAwait(false);

        var result = passage ?? throw new YouVersionApiException(
            System.Net.HttpStatusCode.OK,
            $"The API returned an empty body for passage '{normalizedUsfm}' (version {versionId}).");

        logger.LogDebug("Fetched passage {Usfm} from version {VersionId}.", normalizedUsfm, versionId);
        return result;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string BuildPassageUrl(int versionId, string usfm, PassageRequestOptions options)
    {
        var sb = new StringBuilder("/v1/bibles/");
        sb.Append(versionId);
        sb.Append("/passages/");
        sb.Append(Uri.EscapeDataString(usfm));
        sb.Append("?format=");
        sb.Append(options.Format.ToString().ToLowerInvariant());

        if (options.IncludeHeadings)
            sb.Append("&include_headings=true");

        if (options.IncludeNotes)
            sb.Append("&include_notes=true");

        return sb.ToString();
    }
}
