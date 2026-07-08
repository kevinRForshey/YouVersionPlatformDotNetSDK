using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Platform.API.Models;
using Platform.SDK.Components.BibleComponents;
using Platform.SDK.Services;
using Xunit;

namespace Platform.SDK.Components.Tests.BibleComponents;

public sealed class BookPickerTests : TestContext
{
    private static BibleVersionSummary MakeVersion(int id = 1) => new()
    {
        Id = id,
        Abbreviation = "NIV",
        LocalizedAbbreviation = "NIV",
        Title = "NIV",
        LocalizedTitle = "NIV",
        LanguageTag = "en",
    };

    private static Book MakeBook(string usfm) => new() { Usfm = usfm, Human = usfm, ChapterCount = 1 };

    private readonly Mock<IBookService> _bookService = new();
    private readonly Mock<IBibleReaderStateService> _state = new();

    private IRenderedComponent<BookPicker> RenderPicker()
    {
        Services.AddSingleton(_bookService.Object);
        Services.AddSingleton(_state.Object);
        return RenderComponent<BookPicker>();
    }

    [Fact]
    public void NoVersionSelected_ShowsPrompt()
    {
        _state.SetupGet(s => s.SelectedVersion).Returns((BibleVersionSummary?)null);

        var cut = RenderPicker();

        cut.Markup.Should().Contain("Select a version first.");
    }

    [Fact]
    public void GoldenPath_LoadsBooksForSelectedVersion()
    {
        var version = MakeVersion(3034);
        _state.SetupGet(s => s.SelectedVersion).Returns(version);
        _bookService
            .Setup(s => s.GetBooksAsync(3034, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeBook("GEN"), MakeBook("EXO")]);

        var cut = RenderPicker();

        cut.Markup.Should().Contain("GEN").And.Contain("EXO");
        _bookService.Verify(s => s.GetBooksAsync(3034, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void WhenServiceThrows_ShowsErrorMessage()
    {
        _state.SetupGet(s => s.SelectedVersion).Returns(MakeVersion());
        _bookService
            .Setup(s => s.GetBooksAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderPicker();

        cut.Markup.Should().Contain("Could not load books").And.Contain("boom");
    }

    [Fact]
    public void SameVersionAcrossStateChange_DoesNotReload()
    {
        var version = MakeVersion(1);
        _state.SetupGet(s => s.SelectedVersion).Returns(version);
        _bookService
            .Setup(s => s.GetBooksAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeBook("GEN")]);

        var cut = RenderPicker();
        _state.Raise(s => s.OnStateChanged += null);
        cut.Render();

        _bookService.Verify(s => s.GetBooksAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnStateChanged_VersionChanges_ReloadsBooksForNewVersion()
    {
        var version = MakeVersion(1);
        _state.SetupGet(s => s.SelectedVersion).Returns(version);
        _bookService
            .Setup(s => s.GetBooksAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeBook("GEN")]);

        var cut = RenderPicker();

        var version2 = MakeVersion(2);
        _state.SetupGet(s => s.SelectedVersion).Returns(version2);
        _bookService
            .Setup(s => s.GetBooksAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeBook("MAT")]);

        _state.Raise(s => s.OnStateChanged += null);

        _bookService.Verify(s => s.GetBooksAsync(2, It.IsAny<CancellationToken>()), Times.Once);
        cut.Markup.Should().Contain("MAT");
    }

    [Fact]
    public void OnBookChanged_MatchingUsfm_CallsStateSelectBook()
    {
        var version = MakeVersion();
        var gen = MakeBook("GEN");
        _state.SetupGet(s => s.SelectedVersion).Returns(version);
        _bookService
            .Setup(s => s.GetBooksAsync(version.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([gen, MakeBook("EXO")]);

        var cut = RenderPicker();
        cut.Find("select").Change("GEN");

        _state.Verify(s => s.SelectBook(gen), Times.Once);
    }

    [Fact]
    public void OnBookChanged_NoMatch_DoesNotCallSelectBook()
    {
        var version = MakeVersion();
        _state.SetupGet(s => s.SelectedVersion).Returns(version);
        _bookService
            .Setup(s => s.GetBooksAsync(version.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeBook("GEN")]);

        var cut = RenderPicker();
        cut.Find("select").Change("");

        _state.Verify(s => s.SelectBook(It.IsAny<Book>()), Times.Never);
    }

    [Fact]
    public void Dispose_UnsubscribesFromStateChangedEvent()
    {
        var version = MakeVersion();
        _state.SetupGet(s => s.SelectedVersion).Returns(version);
        _bookService
            .Setup(s => s.GetBooksAsync(version.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeBook("GEN")]);

        var cut = RenderPicker();
        cut.Instance.Dispose();

        var act = () => _state.Raise(s => s.OnStateChanged += null);
        act.Should().NotThrow();
        _bookService.Verify(s => s.GetBooksAsync(version.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
