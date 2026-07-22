using Platform.API.Models;

namespace Platform.SDK.Services
{
    /// <summary>Retrieves the books contained in a Bible version.</summary>
    public interface IBookService
    {
        /// <summary>Retrieves the books in a Bible version.</summary>
        /// <param name="versionId">The numeric Bible version id.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The books contained in the version.</returns>
        Task<IReadOnlyList<Book>> GetBooksAsync(
            int versionId,
            CancellationToken cancellationToken = default);
    }
}
