using System.Net;
using Platform.API.Clients;
using Platform.API.Exceptions;
using Platform.API.Models;
using YouVersion.UsfmReferences;

namespace Platform.SDK.Services
{
    /// <inheritdoc/>
    /// <param name="client">The highlight API client used to read and write highlights.</param>
    public sealed class HighlightService(IHighlightClient client) : IHighlightService
    {
        private const string AccessDeniedMessage =
            "Highlights access isn't available. Please sign in and grant highlights permission when prompted.";

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Highlight>> GetHighlightsAsync(
            int bibleId,
            Reference passage,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await client.GetHighlightsAsync(bibleId, passage, cancellationToken);
            }
            catch (YouVersionApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new HighlightAccessDeniedException(AccessDeniedMessage, ex);
            }
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> GetRecentColorsAsync(CancellationToken cancellationToken = default)
            => client.GetRecentColorsAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task<Highlight> CreateOrUpdateHighlightAsync(
            int bibleId,
            Reference passage,
            string color,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await client.CreateOrUpdateHighlightAsync(bibleId, passage, color, cancellationToken);
            }
            catch (YouVersionApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new HighlightAccessDeniedException(AccessDeniedMessage, ex);
            }
        }

        /// <inheritdoc/>
        public async Task ClearHighlightsAsync(int bibleId, Reference passage, CancellationToken cancellationToken = default)
        {
            try
            {
                await client.ClearHighlightsAsync(bibleId, passage, cancellationToken);
            }
            catch (YouVersionApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new HighlightAccessDeniedException(AccessDeniedMessage, ex);
            }
        }
    }
}
