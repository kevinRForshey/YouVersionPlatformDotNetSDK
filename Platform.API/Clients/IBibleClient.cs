using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Platform.API.Models;

namespace Platform.API.Clients;

/// <summary>
/// Provides discovery and metadata operations for Bible versions and their structure.
/// </summary>
public interface IBibleClient
{
    /// <summary>
    /// Returns a paginated list of Bible versions visible to the current app key.
    /// </summary>
    /// <param name="languageRange">
    /// BCP-47 language range to filter results (e.g. <c>en</c>, <c>es</c>, <c>*</c>).
    /// Defaults to <c>en</c>.
    /// </param>
    /// <param name="pageToken">
    /// Opaque token from a previous response's <see cref="PagedResult{T}.NextPageToken"/>
    /// to retrieve the next page. Pass <see langword="null"/> for the first page.
    /// </param>
    /// <param name="pageSize">
    /// Maximum number of items to return. Pass <see langword="null"/> to use the API default.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A paged result containing <see cref="BibleVersionSummary"/> items.</returns>
    Task<PagedResult<BibleVersionSummary>> GetVersionsAsync(
        string languageRange = "en",
        string? pageToken = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns full metadata for a single Bible version, including its list of available books.
    /// </summary>
    /// <param name="versionId">The numeric Bible version id (e.g. <c>3034</c> for BSB).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The <see cref="BibleVersion"/> metadata.</returns>
    Task<BibleVersion> GetVersionAsync(int versionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full book/chapter/verse structure for a Bible version, as reported by the
    /// API's <c>/index</c> endpoint. This is the authoritative source for real, per-version
    /// counts — use it directly when you need more than the flattened views
    /// <see cref="GetBooksAsync(int, CancellationToken)"/>, <see cref="GetChaptersAsync"/>, or
    /// <see cref="GetVersesAsync"/> provide (e.g. canon, book titles, or intro sections).
    /// </summary>
    /// <param name="versionId">The numeric Bible version id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The <see cref="BibleIndex"/> for the version.</returns>
    Task<BibleIndex> GetIndexAsync(int versionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the books available in a given Bible version, with real per-version chapter
    /// counts sourced from the version's index.
    /// </summary>
    /// <param name="versionId">The numeric Bible version id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="Book"/> records, in canonical order.</returns>
    Task<IReadOnlyList<Book>> GetBooksAsync(int versionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the chapters of a single book in a given Bible version, with real per-chapter
    /// verse counts sourced from the version's index.
    /// </summary>
    /// <param name="versionId">The numeric Bible version id.</param>
    /// <param name="bookUsfm">The USFM book code (e.g. <c>GEN</c>).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="Chapter"/> records, in order.</returns>
    Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        int versionId, string bookUsfm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the verses of a single chapter in a given Bible version.
    /// </summary>
    /// <param name="versionId">The numeric Bible version id.</param>
    /// <param name="bookUsfm">The USFM book code (e.g. <c>GEN</c>).</param>
    /// <param name="chapterNumber">The 1-based chapter number within the book.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A read-only list of <see cref="Verse"/> records, in order. These carry verse numbers
    /// and USFM references only — <see cref="Verse.Text"/> is empty. Use
    /// <see cref="IPassageClient"/> to fetch scripture content.
    /// </returns>
    Task<IReadOnlyList<Verse>> GetVersesAsync(
        int versionId, string bookUsfm, int chapterNumber, CancellationToken cancellationToken = default);
}
