using FluentAssertions;
using Platform.API.Models;
using Xunit;

namespace Platform.SDK.Services.Tests;

public sealed class BibleReaderStateServiceTests
{
    private static BibleVersionSummary MakeVersion(int id = 1) => new()
    {
        Id = id,
        Abbreviation = "NIV",
        LocalizedAbbreviation = "NIV",
        Title = "New International Version",
        LocalizedTitle = "New International Version",
        LanguageTag = "en",
    };

    private static Book MakeBook(string usfm = "GEN") => new()
    {
        Usfm = usfm,
        Human = "Genesis",
        ChapterCount = 50,
    };

    // --- Golden paths ---

    [Fact]
    public void SelectVersion_SetsSelectedVersionAndFiresEvent()
    {
        var sut = new BibleReaderStateService();
        var fired = 0;
        sut.OnStateChanged += () => fired++;
        var version = MakeVersion();

        sut.SelectVersion(version);

        sut.SelectedVersion.Should().Be(version);
        fired.Should().Be(1);
    }

    [Fact]
    public void SelectBook_SetsSelectedBookAndFiresEvent()
    {
        var sut = new BibleReaderStateService();
        sut.SelectVersion(MakeVersion());
        var fired = 0;
        sut.OnStateChanged += () => fired++;
        var book = MakeBook();

        sut.SelectBook(book);

        sut.SelectedBook.Should().Be(book);
        fired.Should().Be(1);
    }

    [Fact]
    public void SelectChapter_SetsSelectedChapterAndFiresEvent()
    {
        var sut = new BibleReaderStateService();
        sut.SelectVersion(MakeVersion());
        sut.SelectBook(MakeBook());
        var fired = 0;
        sut.OnStateChanged += () => fired++;

        sut.SelectChapter(3);

        sut.SelectedChapter.Should().Be(3);
        fired.Should().Be(1);
    }

    [Fact]
    public void SelectVerseRange_SetsBothStartAndEndAndFiresEvent()
    {
        var sut = new BibleReaderStateService();
        sut.SelectVersion(MakeVersion());
        sut.SelectBook(MakeBook());
        sut.SelectChapter(1);
        var fired = 0;
        sut.OnStateChanged += () => fired++;

        sut.SelectVerseRange(1, 5);

        sut.SelectedVerseStart.Should().Be(1);
        sut.SelectedVerseEnd.Should().Be(5);
        fired.Should().Be(1);
    }

    [Fact]
    public void SelectVerseRange_WithNullEnd_RepresentsSingleVerse()
    {
        var sut = new BibleReaderStateService();

        sut.SelectVerseRange(7, null);

        sut.SelectedVerseStart.Should().Be(7);
        sut.SelectedVerseEnd.Should().BeNull();
    }

    // --- Cascading reset behavior ---

    [Fact]
    public void SelectVersion_ClearsBookChapterAndVerses()
    {
        var sut = new BibleReaderStateService();
        sut.SelectVersion(MakeVersion(1));
        sut.SelectBook(MakeBook());
        sut.SelectChapter(2);
        sut.SelectVerseRange(3, 4);

        sut.SelectVersion(MakeVersion(2));

        sut.SelectedBook.Should().BeNull();
        sut.SelectedChapter.Should().BeNull();
        sut.SelectedVerseStart.Should().BeNull();
        sut.SelectedVerseEnd.Should().BeNull();
    }

    [Fact]
    public void SelectBook_ClearsChapterAndVersesButNotVersion()
    {
        var sut = new BibleReaderStateService();
        var version = MakeVersion();
        sut.SelectVersion(version);
        sut.SelectBook(MakeBook("GEN"));
        sut.SelectChapter(2);
        sut.SelectVerseRange(3, 4);

        sut.SelectBook(MakeBook("EXO"));

        sut.SelectedVersion.Should().Be(version);
        sut.SelectedChapter.Should().BeNull();
        sut.SelectedVerseStart.Should().BeNull();
        sut.SelectedVerseEnd.Should().BeNull();
    }

    [Fact]
    public void SelectChapter_ClearsVersesButNotVersionOrBook()
    {
        var sut = new BibleReaderStateService();
        var version = MakeVersion();
        var book = MakeBook();
        sut.SelectVersion(version);
        sut.SelectBook(book);
        sut.SelectChapter(1);
        sut.SelectVerseRange(3, 4);

        sut.SelectChapter(2);

        sut.SelectedVersion.Should().Be(version);
        sut.SelectedBook.Should().Be(book);
        sut.SelectedVerseStart.Should().BeNull();
        sut.SelectedVerseEnd.Should().BeNull();
    }

    [Fact]
    public void Reset_ClearsEverythingAndFiresEvent()
    {
        var sut = new BibleReaderStateService();
        sut.SelectVersion(MakeVersion());
        sut.SelectBook(MakeBook());
        sut.SelectChapter(1);
        sut.SelectVerseRange(1, 2);

        var fired = 0;
        sut.OnStateChanged += () => fired++;
        sut.Reset();

        sut.SelectedVersion.Should().BeNull();
        sut.SelectedBook.Should().BeNull();
        sut.SelectedChapter.Should().BeNull();
        sut.SelectedVerseStart.Should().BeNull();
        sut.SelectedVerseEnd.Should().BeNull();
        fired.Should().Be(1);
    }

    // --- Edge cases ---

    [Fact]
    public void MutatingMethods_WithNoSubscribers_DoNotThrow()
    {
        var sut = new BibleReaderStateService();

        var act = () =>
        {
            sut.SelectVersion(MakeVersion());
            sut.SelectBook(MakeBook());
            sut.SelectChapter(1);
            sut.SelectVerseRange(1, 1);
            sut.Reset();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void OnStateChanged_SupportsMultipleSubscribers_AllAreInvoked()
    {
        var sut = new BibleReaderStateService();
        var firstFired = false;
        var secondFired = false;
        sut.OnStateChanged += () => firstFired = true;
        sut.OnStateChanged += () => secondFired = true;

        sut.SelectVersion(MakeVersion());

        firstFired.Should().BeTrue();
        secondFired.Should().BeTrue();
    }

    [Fact]
    public void OnStateChanged_UnsubscribedHandler_IsNotInvoked()
    {
        var sut = new BibleReaderStateService();
        var fired = 0;
        void Handler() => fired++;

        sut.OnStateChanged += Handler;
        sut.OnStateChanged -= Handler;
        sut.SelectVersion(MakeVersion());

        fired.Should().Be(0);
    }

    [Fact]
    public void NewInstance_HasNoSelectionsByDefault()
    {
        var sut = new BibleReaderStateService();

        sut.SelectedVersion.Should().BeNull();
        sut.SelectedBook.Should().BeNull();
        sut.SelectedChapter.Should().BeNull();
        sut.SelectedVerseStart.Should().BeNull();
        sut.SelectedVerseEnd.Should().BeNull();
    }
}
