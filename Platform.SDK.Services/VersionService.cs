using Platform.API.Clients;
using Platform.API.Models;

namespace Platform.SDK.Services
{
    /// <inheritdoc/>
    /// <param name="client">The Bible API client used to retrieve versions.</param>
    public sealed class VersionService(IBibleClient client) : IVersionService
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<BibleVersionSummary>> GetVersionsAsync(
            string languageRange = "en",
            CancellationToken cancellationToken = default)
        {
            var all = new List<BibleVersionSummary>();
            string? pageToken = null;

            do
            {
                var page = await client.GetVersionsAsync(languageRange, pageToken, cancellationToken: cancellationToken);
                all.AddRange(page.Data);
                pageToken = page.NextPageToken;
            } while (!string.IsNullOrEmpty(pageToken));

            return all;
        }
    }
}
