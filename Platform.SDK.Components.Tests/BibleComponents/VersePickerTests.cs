using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Platform.API.Models;
using Platform.SDK.Components.BibleComponents;
using Platform.SDK.Services;
using Xunit;

namespace Platform.SDK.Components.Tests.BibleComponents;

public sealed class VersePickerTests : TestContext
{
    private const int FallbackMaxVerse = 176;

    private static BibleVersionSummary MakeVersion(int id = 1) => new()
    {
        Id = id,
        Abbreviation = "NIV",
        LocalizedAbbreviation = "NIV",
        Title = "NIV",
        LocalizedTitle = "NIV",
        LanguageTag = "en",
    };

    private static Book MakeBook(string usfm = "GEN") => new() { Usfm = usfm, Human = usfm, ChapterCount = 50 };

    private readonly Mock<IChapterService> _chapterService = new();
    private readonly Mock<IBibleReaderStateService> _state = new();

    private void SetupSelections(BibleVersionSummary version, Book book, int? chapter)
    {
        _state.SetupGet(s => s.SelectedVersion).Returns(version);
        _state.SetupGet(s => s.SelectedBook).Returns(book);
        _state.SetupGet(s => s.SelectedChapter).Returns(chapter);
    }

    private IRenderedComponent<VersePicker> RenderPicker()
    {
        Services.AddSingleton(_chapterService.Object);
        Services.AddSingleton(_state.Object);
        return RenderComponent<VersePicker>();
    }

    [Fact]
    public void NoChapterSelected_ShowsPrompt()
    {
        SetupSelections(MakeVersion(), MakeBook(), null);
        _chapterService
            .Setup(s => s.GetChaptersAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var cut = RenderPicker();

        cut.Markup.Should().Contain("Select a chapter first.");
    }

    [Fact]
    public void GoldenPath_DefaultRange_UsesRealVerseCountFromChapterData()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Chapter { Usfm = "GEN.1", Human = "Genesis 1", VerseCount = 31 }]);

        var cut = RenderPicker();

        _state.Verify(s => s.SelectVerseRange(1, 31), Times.Once);
        cut.Find("#verse-start").GetAttribute("value").Should().Be("1");
        cut.Find("#verse-end").GetAttribute("value").Should().Be("31");
    }

    [Fact]
    public void WhenNoMatchingChapterData_FallsBackToDefaultMaxVerse()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        RenderPicker();

        _state.Verify(s => s.SelectVerseRange(1, FallbackMaxVerse), Times.Once);
    }

    [Fact]
    public void WhenChapterServiceThrows_ShowsLoadErrorAndUsesFallbackMaxVerse()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderPicker();

        cut.Markup.Should().Contain("Could not load chapter data").And.Contain("boom");
        _state.Verify(s => s.SelectVerseRange(1, FallbackMaxVerse), Times.Once);
    }

    [Fact]
    public void OnStartChanged_ValidValue_CommitsNewStart()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Chapter { Usfm = "GEN.1", Human = "Genesis 1", VerseCount = 31 }]);

        var cut = RenderPicker();
        cut.Find("#verse-start").Change("5");

        _state.Verify(s => s.SelectVerseRange(5, 31), Times.Once);
    }

    [Fact]
    public void OnStartChanged_OutOfRange_ShowsValidationErrorAndDoesNotCommit()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Chapter { Usfm = "GEN.1", Human = "Genesis 1", VerseCount = 31 }]);

        var cut = RenderPicker();
        cut.Find("#verse-start").Change("99");

        cut.Markup.Should().Contain("Please enter a verse number between 1 and 31.");
        _state.Verify(s => s.SelectVerseRange(99, It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public void OnStartChanged_MovingPastCurrentEnd_ClearsEnd()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Chapter { Usfm = "GEN.1", Human = "Genesis 1", VerseCount = 31 }]);

        var cut = RenderPicker();
        cut.Find("#verse-end").Change("10");
        cut.Find("#verse-start").Change("20");

        _state.Verify(s => s.SelectVerseRange(20, null), Times.Once);
    }

    [Fact]
    public void OnEndChanged_ValidValue_CommitsNewEnd()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Chapter { Usfm = "GEN.1", Human = "Genesis 1", VerseCount = 31 }]);

        var cut = RenderPicker();
        cut.Find("#verse-end").Change("20");

        _state.Verify(s => s.SelectVerseRange(1, 20), Times.Once);
    }

    [Fact]
    public void OnEndChanged_BlankValue_ClearsEndAndCommits()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Chapter { Usfm = "GEN.1", Human = "Genesis 1", VerseCount = 31 }]);

        var cut = RenderPicker();
        cut.Find("#verse-end").Change("");

        _state.Verify(s => s.SelectVerseRange(1, null), Times.Once);
    }

    [Fact]
    public void OnEndChanged_BeforeStart_ShowsValidationErrorAndDoesNotCommit()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Chapter { Usfm = "GEN.1", Human = "Genesis 1", VerseCount = 31 }]);

        var cut = RenderPicker();
        cut.Find("#verse-start").Change("10");
        cut.Find("#verse-end").Change("5");

        cut.Markup.Should().Contain("End verse must be between 10 and 31.");
        _state.Verify(s => s.SelectVerseRange(10, 5), Times.Never);
    }

    [Fact]
    public void ClearRange_ResetsToFullSingleVerseAtOne()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Chapter { Usfm = "GEN.1", Human = "Genesis 1", VerseCount = 31 }]);

        var cut = RenderPicker();
        cut.Find("#verse-start").Change("10");
        cut.Find("button").Click();

        _state.Verify(s => s.SelectVerseRange(1, null), Times.Once);
    }

    [Fact]
    public void OnStateChanged_ChapterChanges_ResetsRangeToFullNewChapter()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Chapter { Usfm = "GEN.1", Human = "Genesis 1", VerseCount = 31 },
                new Chapter { Usfm = "GEN.2", Human = "Genesis 2", VerseCount = 25 },
            ]);

        var cut = RenderPicker();
        _state.Verify(s => s.SelectVerseRange(1, 31), Times.Once);

        _state.SetupGet(s => s.SelectedChapter).Returns(2);
        _state.Raise(s => s.OnStateChanged += null);

        _state.Verify(s => s.SelectVerseRange(1, 25), Times.Once);
        cut.Find("#verse-start").GetAttribute("value").Should().Be("1");
    }

    [Fact]
    public void Dispose_UnsubscribesFromStateChangedEvent()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 1);
        _chapterService
            .Setup(s => s.GetChaptersAsync(version.Id, book.Usfm, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var cut = RenderPicker();
        cut.Instance.Dispose();

        var act = () => _state.Raise(s => s.OnStateChanged += null);
        act.Should().NotThrow();
    }
}
