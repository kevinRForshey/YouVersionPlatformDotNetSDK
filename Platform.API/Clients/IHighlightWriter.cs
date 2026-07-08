#region usings
using Platform.API.Models;
using YouVersion.UsfmReferences;
#endregion
namespace Platform.API.Clients;

/// <summary>
/// Write surface of the highlights API. Requires OAuth bearer-token authentication.
/// </summary>
/// <remarks>
/// Consumers that only need to create or clear highlights should depend on this interface.
/// Register <see cref="IHighlightClient"/> in the DI container; it implements both
/// <see cref="IHighlightReader"/> and <see cref="IHighlightWriter"/>.
/// </remarks>
public interface IHighlightWriter
{
    /// <summary>
    /// Creates a highlight for a passage, or updates its color if one already exists.
    /// Requires OAuth bearer-token authentication.
    /// </summary>
    /// <param name="bibleId">The numeric Bible version id.</param>
    /// <param name="passage">
    /// The USFM passage reference to highlight (e.g. <c>JHN.3.16</c>).
    /// Must be a valid <see cref="Reference"/>.
    /// </param>
    /// <param name="color">The highlight color as a hex string (e.g. <c>44aa44</c>), without a leading <c>#</c>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created or updated <see cref="Highlight"/>.</returns>
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
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ClearHighlightsAsync(int bibleId, Reference passage, CancellationToken cancellationToken = default);
}
