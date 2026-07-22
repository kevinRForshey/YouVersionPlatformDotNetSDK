using Platform.API.Models;

namespace Platform.SDK.Services
{
    /// <summary>Retrieves the Bible versions available for a language.</summary>
    public interface IVersionService
    {
        /// <summary>
        /// Retrieves all available Bible versions for a language range, transparently paging
        /// through the underlying API until every page has been collected.
        /// </summary>
        /// <param name="languageRange">The BCP-47 language range to filter versions by (e.g. "en").</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The Bible versions available for <paramref name="languageRange"/>.</returns>
        Task<IReadOnlyList<BibleVersionSummary>> GetVersionsAsync(
            string languageRange = "en",
            CancellationToken cancellationToken = default);
    }
}
