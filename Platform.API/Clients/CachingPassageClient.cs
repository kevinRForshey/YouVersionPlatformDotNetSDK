using Microsoft.Extensions.Caching.Hybrid;
using Platform.API.Configuration;
using Platform.API.Models;
using YouVersion.UsfmReferences;

namespace Platform.API.Clients;

internal sealed class CachingPassageClient(
    PassageClient inner,
    HybridCache cache,
    YouVersionCacheOptions opts) : IPassageClient
{
    public async Task<Passage> GetPassageAsync(
        int versionId,
        Reference usfm,
        PassageRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedOptions = options ?? PassageRequestOptions.Default;
        var key = $"yv:passage:{versionId}:{usfm}:{resolvedOptions.Format}:{resolvedOptions.IncludeHeadings}:{resolvedOptions.IncludeNotes}";
        return await cache.GetOrCreateAsync(
            key,
            ct => new ValueTask<Passage>(inner.GetPassageAsync(versionId, usfm, resolvedOptions, ct)),
            new HybridCacheEntryOptions { Expiration = opts.PassageTtl },
            cancellationToken: cancellationToken);
    }
}
