using Platform.API.Models;

namespace Platform.SDK.Services
{
    /// <summary>
    /// Tracks the Bible version, book, chapter, and verse range currently selected in a reader
    /// UI, and notifies subscribers when the selection changes.
    /// </summary>
    public interface IBibleReaderStateService
    {
        /// <summary>The currently selected Bible version, or <see langword="null"/> if none is selected.</summary>
        BibleVersionSummary? SelectedVersion { get; }

        /// <summary>The currently selected book, or <see langword="null"/> if none is selected.</summary>
        Book? SelectedBook { get; }

        /// <summary>The currently selected chapter number, or <see langword="null"/> if none is selected.</summary>
        int? SelectedChapter { get; }

        /// <summary>The first verse of the currently selected range, or <see langword="null"/> if none is selected.</summary>
        int? SelectedVerseStart { get; }

        /// <summary>The last verse of the currently selected range, or <see langword="null"/> if none is selected.</summary>
        int? SelectedVerseEnd { get; }

        /// <summary>Selects a Bible version, clearing any previously selected book, chapter, and verse range.</summary>
        /// <param name="version">The Bible version to select.</param>
        void SelectVersion(BibleVersionSummary version);

        /// <summary>Selects a book, clearing any previously selected chapter and verse range.</summary>
        /// <param name="book">The book to select.</param>
        void SelectBook(Book book);

        /// <summary>Selects a chapter, clearing any previously selected verse range.</summary>
        /// <param name="chapter">The chapter number to select.</param>
        void SelectChapter(int chapter);

        /// <summary>Selects a verse range within the currently selected chapter.</summary>
        /// <param name="start">The first verse in the range.</param>
        /// <param name="end">The last verse in the range, or <see langword="null"/> for a single verse.</param>
        void SelectVerseRange(int start, int? end);

        /// <summary>Clears the version, book, chapter, and verse range selection.</summary>
        void Reset();

        /// <summary>Raised whenever the selected version, book, chapter, or verse range changes.</summary>
        event Action? OnStateChanged;
    }

}
