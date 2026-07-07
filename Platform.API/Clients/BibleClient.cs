using Microsoft.Extensions.Logging;

using Platform.API.Exceptions;
using Platform.API.Http;
using Platform.API.Models;

using System.Text;

namespace Platform.API.Clients;

/// <summary>
/// Default implementation of <see cref="IBibleClient"/>.
/// </summary>
internal sealed class BibleClient(HttpClient httpClient, ILogger<BibleClient> logger) : IBibleClient
{
    /// <inheritdoc />
    public async Task<PagedResult<BibleVersionSummary>> GetVersionsAsync(
        string languageRange = "en",
        string? pageToken = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(languageRange))
            throw new ArgumentException("Language range is required.", nameof(languageRange));

        if (pageToken is not null && string.IsNullOrWhiteSpace(pageToken))
            throw new ArgumentException("Page token cannot be empty or whitespace.", nameof(pageToken));

        if (pageSize is <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than zero.");

        var url = BuildVersionsUrl(languageRange, pageToken, pageSize);
        logger.LogDebug("Fetching Bible versions for language range '{LanguageRange}' (pageToken={PageToken}).", languageRange, pageToken);

        var result = await ApiRequestHelper.GetJsonAsync<PagedResult<BibleVersionSummary>>(httpClient, url, logger, cancellationToken)
            .ConfigureAwait(false);

        var list = result ?? new PagedResult<BibleVersionSummary>();
        logger.LogDebug("Fetched {Count} Bible version(s) from API.", list.Data.Count);
        return list;
    }

    /// <inheritdoc />
    public async Task<BibleVersion> GetVersionAsync(int versionId, CancellationToken cancellationToken = default)
    {
        if (versionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionId), versionId, "Version id must be greater than zero.");

        logger.LogDebug("Fetching Bible version {VersionId}.", versionId);

        var result = await ApiRequestHelper.GetJsonAsync<BibleVersion>(httpClient, $"/v1/bibles/{versionId}", logger, cancellationToken)
            .ConfigureAwait(false);

        var version = result ?? throw new YouVersionEmptyResponseException(
            $"Bible version {versionId} was not found or returned an empty response.");

        logger.LogDebug("Fetched Bible version {VersionId} ({Abbreviation}).", versionId, version.Abbreviation);
        return version;
    }

    /// <inheritdoc />
    public async Task<BibleIndex> GetIndexAsync(int versionId, CancellationToken cancellationToken = default)
    {
        if (versionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionId), versionId, "Version id must be greater than zero.");

        logger.LogDebug("Fetching index for Bible version {VersionId}.", versionId);

        var result = await ApiRequestHelper.GetJsonAsync<BibleIndex>(httpClient, $"/v1/bibles/{versionId}/index", logger, cancellationToken)
            .ConfigureAwait(false);

        var index = result ?? throw new YouVersionEmptyResponseException(
            $"The index for Bible version {versionId} was not found or returned an empty response.");

        logger.LogDebug("Fetched index for Bible version {VersionId} with {Count} book(s).", versionId, index.Books.Count);
        return index;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Book>> GetBooksAsync(int versionId, CancellationToken cancellationToken = default)
    {
        if (versionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionId), versionId, "Version id must be greater than zero.");

        logger.LogDebug("Fetching books for Bible version {VersionId}.", versionId);

        var index = await GetIndexAsync(versionId, cancellationToken).ConfigureAwait(false);
        var books = BibleIndexMapper.BuildBooks(index);

        logger.LogDebug("Fetched {Count} book(s) for Bible version {VersionId}.", books.Count, versionId);
        return books;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        int versionId, string bookUsfm, CancellationToken cancellationToken = default)
    {
        if (versionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionId), versionId, "Version id must be greater than zero.");

        if (string.IsNullOrWhiteSpace(bookUsfm))
            throw new ArgumentException("Book USFM code is required.", nameof(bookUsfm));

        logger.LogDebug("Fetching chapters for book {BookUsfm} in Bible version {VersionId}.", bookUsfm, versionId);

        var index = await GetIndexAsync(versionId, cancellationToken).ConfigureAwait(false);
        var indexBook = BibleIndexMapper.FindBook(index, versionId, bookUsfm);
        var chapters = BibleIndexMapper.BuildChapters(indexBook);

        logger.LogDebug("Fetched {Count} chapter(s) for book {BookUsfm} in Bible version {VersionId}.", chapters.Count, bookUsfm, versionId);
        return chapters;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Verse>> GetVersesAsync(
        int versionId, string bookUsfm, int chapterNumber, CancellationToken cancellationToken = default)
    {
        if (versionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionId), versionId, "Version id must be greater than zero.");

        if (string.IsNullOrWhiteSpace(bookUsfm))
            throw new ArgumentException("Book USFM code is required.", nameof(bookUsfm));

        if (chapterNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(chapterNumber), chapterNumber, "Chapter number must be greater than zero.");

        logger.LogDebug("Fetching verses for {BookUsfm} {ChapterNumber} in Bible version {VersionId}.", bookUsfm, chapterNumber, versionId);

        var index = await GetIndexAsync(versionId, cancellationToken).ConfigureAwait(false);
        var indexBook = BibleIndexMapper.FindBook(index, versionId, bookUsfm);
        var indexChapter = BibleIndexMapper.FindChapter(indexBook, versionId, bookUsfm, chapterNumber);

        var verses = BibleIndexMapper.BuildVerses(indexBook, indexChapter);

        logger.LogDebug("Fetched {Count} verse(s) for {BookUsfm} {ChapterNumber} in Bible version {VersionId}.", verses.Count, bookUsfm, chapterNumber, versionId);
        return verses;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string BuildVersionsUrl(string languageRange, string? pageToken, int? pageSize)
    {
        var sb = new StringBuilder("/v1/bibles?language_ranges[]=");
        sb.Append(Uri.EscapeDataString(languageRange));

        if (pageToken is not null)
        {
            sb.Append("&page_token=");
            sb.Append(Uri.EscapeDataString(pageToken));
        }

        if (pageSize.HasValue)
        {
            sb.Append("&page_size=");
            sb.Append(pageSize.Value);
        }

        return sb.ToString();
    }
}
