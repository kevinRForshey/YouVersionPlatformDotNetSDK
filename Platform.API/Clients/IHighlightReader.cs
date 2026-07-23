using Platform.API.Models;
using BiblePlatform.UsfmReferences;

namespace Platform.API.Clients;

/// <summary>
/// Read-only surface of the highlights API. Requires only app-key authentication.
/// </summary>
/// <remarks>
/// Consumers that only need to read highlights should depend on this interface
/// rather than <see cref="IHighlightClient"/> to minimise their surface area.
/// </remarks>
public interface IHighlightReader
{
    /// <summary>
    /// Returns the highlights within a passage. Passing a whole-chapter reference returns one
    /// entry per highlighted verse in that chapter.
    /// </summary>
    /// <param name="bibleId">The numeric Bible version id.</param>
    /// <param name="passage">The USFM passage to look up highlights within (e.g. a chapter or a single verse).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The highlights found within <paramref name="passage"/>.</returns>
    Task<IReadOnlyList<Highlight>> GetHighlightsAsync(
        int bibleId,
        Reference passage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the colors the current user has most recently used for highlights.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Recently used highlight colors as hex strings (e.g. <c>44aa44</c>).</returns>
    Task<IReadOnlyList<string>> GetRecentColorsAsync(CancellationToken cancellationToken = default);
}
