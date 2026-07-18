using Platform.API.Models;
using YouVersion.UsfmReferences;

namespace Platform.SDK.Services
{
    /// <summary>Reads and writes highlights for Bible passages.</summary>
    public interface IHighlightService
    {
        /// <summary>
        /// Returns the highlights within a passage. Passing a whole-chapter reference returns one
        /// entry per highlighted verse in that chapter.
        /// </summary>
        /// <param name="bibleId">The numeric Bible version id.</param>
        /// <param name="passage">The USFM passage to look up highlights within.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The highlights found within <paramref name="passage"/>.</returns>
        Task<IReadOnlyList<Highlight>> GetHighlightsAsync(
            int bibleId,
            Reference passage,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the colors the current user has most recently used for highlights.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Recently used highlight colors as hex strings (e.g. "44aa44").</returns>
        Task<IReadOnlyList<string>> GetRecentColorsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a highlight for a passage, or updates its color if one already exists.
        /// </summary>
        /// <param name="bibleId">The numeric Bible version id.</param>
        /// <param name="passage">The USFM passage reference to highlight (e.g. "JHN.3.16").</param>
        /// <param name="color">The highlight color as a hex string (e.g. "44aa44"), without a leading '#'.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The created or updated highlight.</returns>
        Task<Highlight> CreateOrUpdateHighlightAsync(
            int bibleId,
            Reference passage,
            string color,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears any highlight(s) for a passage.
        /// </summary>
        /// <param name="bibleId">The numeric Bible version id.</param>
        /// <param name="passage">The USFM passage to clear highlights from.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        Task ClearHighlightsAsync(int bibleId, Reference passage, CancellationToken cancellationToken = default);
    }
}
