using Microsoft.AspNetCore.Components;
using Platform.API.Models;

namespace Platform.SDK.Components.BibleComponents
{
    public partial class VersePicker
    {
        [Parameter, EditorRequired] public string Book { get; set; } = string.Empty;
        [Parameter, EditorRequired] public int Chapter { get; set; }
        [Parameter] public int Verse { get; set; }       // maps from SelectedVerseStart
        [Parameter] public int VerseEnd { get; set; }    // maps from SelectedVerseEnd
        [Parameter, EditorRequired] public int VersionId { get; set; }

        // Used only until the real per-chapter verse count has loaded from the API.
        private const int FallbackMaxVerse = 176; // Psalm 119 — longest chapter

        private IReadOnlyList<Chapter> _chapters = [];
        private int _loadedForVersion;
        private string? _loadedForBook;

        private int _verseStart = 1;
        private int? _verseEnd;
        private string? _validationError;
        private string? _loadError;
        private int? _lastChapter;
        private bool _defaultRangeApplied;

        // Matched by USFM (e.g. "JAS.1") rather than list position, so the lookup
        // is correct even if the index ever returns chapters out of order.
        private Chapter? CurrentChapterInfo
            => State.SelectedBook is { } book && State.SelectedChapter is { } chapterNumber
                ? _chapters.FirstOrDefault(c => c.Usfm == $"{book.Usfm}.{chapterNumber}")
                : null;

        private int MaxVerse => CurrentChapterInfo?.VerseCount ?? FallbackMaxVerse;

        protected override void OnInitialized()
        {
            // Seed _lastChapter from the current state before subscribing.
            // VersePicker only mounts after a chapter is already selected, so
            // _lastChapter would otherwise stay null and the first notification
            // (even one the user triggers via verse input) would falsely look like
            // a chapter change and reset _verseStart back to 1.
            _lastChapter = State.SelectedChapter;
            State.OnStateChanged += OnStateChangedHandler;
        }

        protected override async Task OnParametersSetAsync()
        {
            await LoadChaptersIfNeededAsync();

            // VersePicker only mounts once a chapter is already selected, so the
            // "chapter changed" branch in OnStateChangedHandler never fires for that
            // initial chapter — apply the full-chapter default here instead, once,
            // after the real per-chapter verse count has had a chance to load.
            if (!_defaultRangeApplied && State.SelectedChapter is not null)
            {
                _defaultRangeApplied = true;
                _verseStart = 1;
                _verseEnd = MaxVerse;
                State.SelectVerseRange(_verseStart, _verseEnd);
            }
        }

        private void OnStateChangedHandler()
            => InvokeAsync(async () =>
            {
                await LoadChaptersIfNeededAsync();

                if (State.SelectedChapter != _lastChapter)
                {
                    _lastChapter = State.SelectedChapter;
                    _verseStart = 1;
                    _verseEnd = State.SelectedChapter is not null ? MaxVerse : null;
                    _validationError = null;
                    if (State.SelectedChapter is not null)
                        State.SelectVerseRange(_verseStart, _verseEnd);
                }
                StateHasChanged();
            });

        private async Task LoadChaptersIfNeededAsync()
        {
            var version = State.SelectedVersion;
            var book = State.SelectedBook;
            if (version is null || book is null) return;
            if (version.Id == _loadedForVersion && book.Usfm == _loadedForBook) return;

            _loadedForVersion = version.Id;
            _loadedForBook = book.Usfm;
            _loadError = null;

            try
            {
                _chapters = await ChapterService.GetChaptersAsync(version.Id, book.Usfm);
            }
            catch (Exception ex)
            {
                _chapters = [];
                _loadError = $"Could not load chapter data ({ex.Message}); verse range is not being limited to this chapter.";
            }
        }

        private void OnStartChanged(ChangeEventArgs e)
        {
            _validationError = null;
            if (int.TryParse(e.Value?.ToString(), out var v) && v >= 1 && v <= MaxVerse)
            {
                _verseStart = v;
                if (_verseEnd.HasValue && _verseEnd < _verseStart)
                    _verseEnd = null;
                Commit();
            }
            else
            {
                _validationError = $"Please enter a verse number between 1 and {MaxVerse}.";
            }
        }

        private void OnEndChanged(ChangeEventArgs e)
        {
            _validationError = null;
            var raw = e.Value?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                _verseEnd = null;
                Commit();
                return;
            }

            if (int.TryParse(raw, out var v) && v >= _verseStart && v <= MaxVerse)
            {
                _verseEnd = v;
                Commit();
            }
            else
            {
                _validationError = $"End verse must be between {_verseStart} and {MaxVerse}.";
            }
        }

        private void ClearRange()
        {
            _verseStart = 1;
            _verseEnd = null;
            _validationError = null;
            Commit();
        }

        private void Commit() => State.SelectVerseRange(_verseStart, _verseEnd);

        public void Dispose()
            => State.OnStateChanged -= OnStateChangedHandler;
    }
}
