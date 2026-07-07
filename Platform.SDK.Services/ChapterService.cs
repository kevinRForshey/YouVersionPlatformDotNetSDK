using Platform.API.Clients;
using Platform.API.Models;

namespace Platform.SDK.Services
{
    public sealed class ChapterService(IBibleClient client) : IChapterService
    {
        public Task<IReadOnlyList<Chapter>> GetChaptersAsync(
            int versionId,
            string bookUsfm,
            CancellationToken cancellationToken = default)
            => client.GetChaptersAsync(versionId, bookUsfm, cancellationToken);
    }
}
