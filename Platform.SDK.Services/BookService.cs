using Platform.API.Clients;
using Platform.API.Models;

namespace Platform.SDK.Services
{
    /// <inheritdoc/>
    /// <param name="client">The Bible API client used to retrieve books.</param>
    public sealed class BookService(IBibleClient client) : IBookService
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<Book>> GetBooksAsync(
            int versionId,
            CancellationToken cancellationToken = default)
            => client.GetBooksAsync(versionId, cancellationToken);
    }
}
