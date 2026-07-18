using Platform.API.Models;

namespace Platform.SDK.Services
{
    /// <inheritdoc/>
    public sealed class BibleReaderStateService : IBibleReaderStateService
    {
        /// <inheritdoc/>
        public BibleVersionSummary? SelectedVersion { get; private set; }
        /// <inheritdoc/>
        public Book? SelectedBook { get; private set; }
        /// <inheritdoc/>
        public int? SelectedChapter { get; private set; }
        /// <inheritdoc/>
        public int? SelectedVerseStart { get; private set; }
        /// <inheritdoc/>
        public int? SelectedVerseEnd { get; private set; }

        /// <inheritdoc/>
        public event Action? OnStateChanged;

        /// <inheritdoc/>
        public void SelectVersion(BibleVersionSummary version)
        {
            SelectedVersion = version;
            SelectedBook = null;
            SelectedChapter = null;
            SelectedVerseStart = null;
            SelectedVerseEnd = null;
            NotifyStateChanged();
        }

        /// <inheritdoc/>
        public void SelectBook(Book book)
        {
            SelectedBook = book;
            SelectedChapter = null;
            SelectedVerseStart = null;
            SelectedVerseEnd = null;
            NotifyStateChanged();
        }

        /// <inheritdoc/>
        public void SelectChapter(int chapter)
        {
            SelectedChapter = chapter;
            SelectedVerseStart = null;
            SelectedVerseEnd = null;
            NotifyStateChanged();
        }

        /// <inheritdoc/>
        public void SelectVerseRange(int start, int? end)
        {
            SelectedVerseStart = start;
            SelectedVerseEnd = end;
            NotifyStateChanged();
        }

        /// <inheritdoc/>
        public void Reset()
        {
            SelectedVersion = null;
            SelectedBook = null;
            SelectedChapter = null;
            SelectedVerseStart = null;
            SelectedVerseEnd = null;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnStateChanged?.Invoke();
    }
}
