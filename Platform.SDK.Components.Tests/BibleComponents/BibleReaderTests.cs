using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Platform.API.Models;
using Platform.SDK.Components.BibleComponents;
using Platform.SDK.Services;
using Xunit;
using YouVersion.UsfmReferences;

namespace Platform.SDK.Components.Tests.BibleComponents;

public sealed class BibleReaderTests : TestContext
{
    private static BibleVersionSummary MakeVersion(int id = 3034) => new()
    {
        Id = id,
        Abbreviation = "NIV",
        LocalizedAbbreviation = "NIV",
        Title = "NIV",
        LocalizedTitle = "NIV",
        LanguageTag = "en",
        Copyright = "© 2011 Publisher",
    };

    private static Book MakeBook(string usfm = "JHN") => new() { Usfm = usfm, Human = "John", ChapterCount = 21 };

    private static Passage MakePassage() => new() { Id = "JHN.3.16", Content = "For God so loved...", Reference = "John 3:16" };

    private readonly Mock<IBibleReaderStateService> _state = new();
    private readonly Mock<IPassageService> _passageService = new();
    private readonly Mock<IVersionService> _versionService = new();
    private readonly Mock<IBookService> _bookService = new();
    private readonly Mock<IChapterService> _chapterService = new();
    private readonly Mock<IAuthSessionService> _authSessionService = new();
    private readonly Mock<IHighlightService> _highlightService = new();

    private void SetupSelections(BibleVersionSummary? version, Book? book, int? chapter, int? verseStart, int? verseEnd = null)
    {
        _state.SetupGet(s => s.SelectedVersion).Returns(version);
        _state.SetupGet(s => s.SelectedBook).Returns(book);
        _state.SetupGet(s => s.SelectedChapter).Returns(chapter);
        _state.SetupGet(s => s.SelectedVerseStart).Returns(verseStart);
        _state.SetupGet(s => s.SelectedVerseEnd).Returns(verseEnd);
    }

    private IRenderedComponent<BibleReader> RenderReader(Action<ComponentParameterCollectionBuilder<BibleReader>>? configure = null)
    {
        _versionService.Setup(s => s.GetVersionsAsync("en", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _bookService.Setup(s => s.GetBooksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _chapterService.Setup(s => s.GetChaptersAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _authSessionService.Setup(s => s.GetCurrentSessionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AuthSession.SignedOut);
        _highlightService
            .Setup(s => s.GetHighlightsAsync(It.IsAny<int>(), It.IsAny<Reference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Highlight>)[]);

        Services.AddSingleton(_state.Object);
        Services.AddSingleton(_passageService.Object);
        Services.AddSingleton(_versionService.Object);
        Services.AddSingleton(_bookService.Object);
        Services.AddSingleton(_chapterService.Object);
        Services.AddSingleton(_authSessionService.Object);
        Services.AddSingleton(_highlightService.Object);

        return configure is null ? RenderComponent<BibleReader>() : RenderComponent<BibleReader>(configure);
    }

    [Fact]
    public void IncompleteSelection_ReadButtonIsDisabled()
    {
        SetupSelections(MakeVersion(), MakeBook(), 3, null);

        var cut = RenderReader();

        cut.Find("button.read-passage-button.w-100").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void CompleteSelection_ReadButtonIsEnabledAndPromptShown()
    {
        SetupSelections(MakeVersion(), MakeBook(), 3, 16);

        var cut = RenderReader();

        cut.Find("button.read-passage-button.w-100").HasAttribute("disabled").Should().BeFalse();
        cut.Markup.Should().Contain("Press").And.Contain("Read");
    }

    [Fact]
    public async Task ClickingRead_RequestsPassageWithCurrentSelectionAndRendersIt()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 3, 16, 17);
        _passageService
            .Setup(s => s.GetPassageAsync(version.Id, book.Usfm, 3, 16, 17, It.IsAny<PassageRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePassage());

        var cut = RenderReader();
        await cut.Find("button.read-passage-button.w-100").ClickAsync(new MouseEventArgs());

        _passageService.Verify(
            s => s.GetPassageAsync(version.Id, book.Usfm, 3, 16, 17, It.IsAny<PassageRequestOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
        cut.Markup.Should().Contain("John 3:16");
        cut.Markup.Should().Contain("For God so loved");
        cut.Markup.Should().Contain("© 2011 Publisher");
    }

    [Fact]
    public async Task ClickingRead_InvokesOnPassageLoadedCallback()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 3, 16);
        _passageService
            .Setup(s => s.GetPassageAsync(version.Id, book.Usfm, 3, 16, null, It.IsAny<PassageRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePassage());

        Passage? loaded = null;
        var cut = RenderReader(p => p.Add(
            x => x.OnPassageLoaded,
            EventCallback.Factory.Create<Passage>(this, passage => loaded = passage)));

        await cut.Find("button.read-passage-button.w-100").ClickAsync(new MouseEventArgs());

        loaded.Should().NotBeNull();
        loaded!.Reference.Should().Be("John 3:16");
    }

    [Fact]
    public async Task ClickingRead_WhenServiceThrows_ShowsErrorAlert()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 3, 16);
        _passageService
            .Setup(s => s.GetPassageAsync(version.Id, book.Usfm, 3, 16, null, It.IsAny<PassageRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderReader();
        await cut.Find("button.read-passage-button.w-100").ClickAsync(new MouseEventArgs());

        cut.Markup.Should().Contain("alert-danger").And.Contain("Could not load passage").And.Contain("boom");
    }

    [Fact]
    public async Task ClickingReadTwiceInSuccession_CancelsThePriorRequest()
    {
        var version = MakeVersion();
        var book = MakeBook();
        SetupSelections(version, book, 3, 16);

        var firstCallTcs = new TaskCompletionSource<Passage>();
        CancellationToken? firstToken = null;
        var callCount = 0;

        _passageService
            .Setup(s => s.GetPassageAsync(version.Id, book.Usfm, 3, 16, null, It.IsAny<PassageRequestOptions>(), It.IsAny<CancellationToken>()))
            .Returns((int _, string _, int _, int _, int? _, PassageRequestOptions? _, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    firstToken = ct;
                    return firstCallTcs.Task;
                }

                return Task.FromResult(MakePassage());
            });

        var cut = RenderReader();
        var button = cut.Find("button.read-passage-button.w-100");

        var firstClick = button.ClickAsync(new MouseEventArgs());
        var secondClick = button.ClickAsync(new MouseEventArgs());

        firstToken.Should().NotBeNull();
        firstToken!.Value.IsCancellationRequested.Should().BeTrue();

        firstCallTcs.SetCanceled();
        await Task.WhenAll(firstClick, secondClick);
    }

    [Fact]
    public void Dispose_UnsubscribesFromStateChangedEvent()
    {
        SetupSelections(null, null, null, null);
        var cut = RenderReader();
        cut.Instance.Dispose();

        var act = () => _state.Raise(s => s.OnStateChanged += null);
        act.Should().NotThrow();
    }
}
