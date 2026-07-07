using Platform.API.Models;

namespace Platform.SDK.Services
{
    public interface IChapterService
    {
        Task<IReadOnlyList<Chapter>> GetChaptersAsync(
            int versionId,
            string bookUsfm,
            CancellationToken cancellationToken = default);
    }
}
