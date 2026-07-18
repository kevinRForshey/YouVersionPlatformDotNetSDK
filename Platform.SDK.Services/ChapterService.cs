using Platform.API.Clients;
using Platform.API.Models;

namespace Platform.SDK.Services
{
    /// <inheritdoc/>
    /// <param name="client">The Bible API client used to retrieve chapters.</param>
    public sealed class ChapterService(IBibleClient client) : IChapterService
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<Chapter>> GetChaptersAsync(
            int versionId,
            string bookUsfm,
            CancellationToken cancellationToken = default)
            => client.GetChaptersAsync(versionId, bookUsfm, cancellationToken);
    }
}
