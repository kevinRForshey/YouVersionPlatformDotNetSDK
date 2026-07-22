using Platform.API.Models;

namespace Platform.SDK.Services
{
    /// <summary>Retrieves the chapters contained in a book.</summary>
    public interface IChapterService
    {
        /// <summary>Retrieves the chapters in a book.</summary>
        /// <param name="versionId">The numeric Bible version id.</param>
        /// <param name="bookUsfm">The USFM book id (e.g. "JHN").</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The chapters contained in the book.</returns>
        Task<IReadOnlyList<Chapter>> GetChaptersAsync(
            int versionId,
            string bookUsfm,
            CancellationToken cancellationToken = default);
    }
}
