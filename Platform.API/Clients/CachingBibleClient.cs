using Microsoft.Extensions.Caching.Hybrid;
using Platform.API.Configuration;
using Platform.API.Models;

namespace Platform.API.Clients;

/// <summary>
/// Caches <see cref="IBibleClient"/> reads behind <see cref="HybridCache"/>. The full
/// <see cref="BibleIndex"/> for a version is cached once and sliced in-memory for
/// <see cref="GetBooksAsync"/>, <see cref="GetChaptersAsync"/>, and <see cref="GetVersesAsync"/>,
/// so looking up a single book or chapter never issues more than one cached fetch per version.
/// </summary>
internal sealed class CachingBibleClient(
    BibleClient inner,
    HybridCache cache,
    YouVersionCacheOptions opts) : IBibleClient
{
    public async Task<PagedResult<BibleVersionSummary>> GetVersionsAsync(
        string languageRange = "en",
        string? pageToken = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var key = $"yv:versions:{languageRange}:{pageToken}:{pageSize}";
        return await cache.GetOrCreateAsync(
            key,
            ct => new ValueTask<PagedResult<BibleVersionSummary>>(
                inner.GetVersionsAsync(languageRange, pageToken, pageSize, ct)),
            new HybridCacheEntryOptions { Expiration = opts.VersionsTtl },
            cancellationToken: cancellationToken);
    }

    public async Task<BibleVersion> GetVersionAsync(int versionId, CancellationToken cancellationToken = default)
    {
        var key = $"yv:version:{versionId}";
        return await cache.GetOrCreateAsync(
            key,
            ct => new ValueTask<BibleVersion>(inner.GetVersionAsync(versionId, ct)),
            new HybridCacheEntryOptions { Expiration = opts.VersionsTtl },
            cancellationToken: cancellationToken);
    }

    public async Task<BibleIndex> GetIndexAsync(int versionId, CancellationToken cancellationToken = default)
    {
        var key = $"yv:index:{versionId}";
        return await cache.GetOrCreateAsync(
            key,
            ct => new ValueTask<BibleIndex>(inner.GetIndexAsync(versionId, ct)),
            new HybridCacheEntryOptions { Expiration = opts.VersionsTtl },
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Book>> GetBooksAsync(int versionId, CancellationToken cancellationToken = default)
    {
        var index = await GetIndexAsync(versionId, cancellationToken).ConfigureAwait(false);
        return BibleIndexMapper.BuildBooks(index);
    }

    public async Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        int versionId, string bookUsfm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bookUsfm))
            throw new ArgumentException("Book USFM code is required.", nameof(bookUsfm));

        var index = await GetIndexAsync(versionId, cancellationToken).ConfigureAwait(false);
        var book = BibleIndexMapper.FindBook(index, versionId, bookUsfm);
        return BibleIndexMapper.BuildChapters(book);
    }

    public async Task<IReadOnlyList<Verse>> GetVersesAsync(
        int versionId, string bookUsfm, int chapterNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bookUsfm))
            throw new ArgumentException("Book USFM code is required.", nameof(bookUsfm));

        if (chapterNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(chapterNumber), chapterNumber, "Chapter number must be greater than zero.");

        var index = await GetIndexAsync(versionId, cancellationToken).ConfigureAwait(false);
        var book = BibleIndexMapper.FindBook(index, versionId, bookUsfm);
        var chapter = BibleIndexMapper.FindChapter(book, versionId, bookUsfm, chapterNumber);
        return BibleIndexMapper.BuildVerses(book, chapter);
    }
}
