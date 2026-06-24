using Platform.API.Models;
using YouVersion.UsfmReferences;

namespace Platform.SDK.Services
{
    public interface IPassageService
    {
        Task<Passage> GetPassageAsync(
            int versionId,
            Reference reference,
            PassageRequestOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}

