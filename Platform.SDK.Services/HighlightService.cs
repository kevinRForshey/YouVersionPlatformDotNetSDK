using Platform.API.Clients;
using Platform.API.Models;
using YouVersion.UsfmReferences;

namespace Platform.SDK.Services
{
    public sealed class HighlightService(IHighlightClient client) : IHighlightService
    {
        public Task<IReadOnlyList<Highlight>> GetHighlightsAsync(
            int bibleId,
            Reference passage,
            CancellationToken cancellationToken = default)
            => client.GetHighlightsAsync(bibleId, passage, cancellationToken);

        public Task<IReadOnlyList<string>> GetRecentColorsAsync(CancellationToken cancellationToken = default)
            => client.GetRecentColorsAsync(cancellationToken);

        public Task<Highlight> CreateOrUpdateHighlightAsync(
            int bibleId,
            Reference passage,
            string color,
            CancellationToken cancellationToken = default)
            => client.CreateOrUpdateHighlightAsync(bibleId, passage, color, cancellationToken);

        public Task ClearHighlightsAsync(int bibleId, Reference passage, CancellationToken cancellationToken = default)
            => client.ClearHighlightsAsync(bibleId, passage, cancellationToken);
    }
}
