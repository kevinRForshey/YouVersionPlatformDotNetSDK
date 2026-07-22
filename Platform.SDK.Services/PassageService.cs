#region usings
using Platform.API.Clients;
using Platform.API.Models;
using YouVersion.UsfmReferences;
#endregion

namespace Platform.SDK.Services
{
    /// <inheritdoc/>
    /// <param name="client">The passage API client used to retrieve passages.</param>
    public sealed class PassageService(IPassageClient client) : IPassageService
    {
        /// <summary>
        /// Retrieves a Bible passage using a typed USFM reference.
        /// </summary>
        /// <param name="versionId">The numeric Bible version id.</param>
        /// <param name="reference">The USFM passage reference.</param>
        /// <param name="options">Optional passage request options (format, headings, notes).</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The requested passage.</returns>
        public Task<Passage> GetPassageAsync(
            int versionId,
            Reference reference,
            PassageRequestOptions? options = null,
            CancellationToken cancellationToken = default)
            => client.GetPassageAsync(versionId, reference, options, cancellationToken);

        /// <summary>
        /// Retrieves a Bible passage from raw book/chapter/verse primitives, building the
        /// <see cref="Reference"/> internally so callers don't have to construct one by hand.
        /// </summary>
        /// <param name="versionId">The numeric Bible version id.</param>
        /// <param name="bookUsfm">The USFM book id (e.g. "JHN").</param>
        /// <param name="chapter">The chapter number.</param>
        /// <param name="verseStart">The first verse in the range.</param>
        /// <param name="verseEnd">The last verse in the range. Defaults to <paramref name="verseStart"/> when omitted.</param>
        /// <param name="options">Optional passage request options (format, headings, notes).</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The requested passage.</returns>
        public Task<Passage> GetPassageAsync(
            int versionId,
            string bookUsfm,
            int chapter,
            int verseStart,
            int? verseEnd = null,
            PassageRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var reference = new Reference(
                book: bookUsfm,
                chapter: chapter,
                verses: [new VerseRange(verseStart, verseEnd ?? verseStart)]);

            return GetPassageAsync(versionId, reference, options, cancellationToken);
        }
    }
}
