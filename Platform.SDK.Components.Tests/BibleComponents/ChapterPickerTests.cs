using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Platform.API.Models;
using Platform.SDK.Components.BibleComponents;
using Platform.SDK.Services;
using Xunit;

namespace Platform.SDK.Components.Tests.BibleComponents;

public sealed class ChapterPickerTests : TestContext
{
    private readonly Mock<IBibleReaderStateService> _state = new();

    private IRenderedComponent<ChapterPicker> RenderPicker()
    {
        Services.AddSingleton(_state.Object);
        return RenderComponent<ChapterPicker>();
    }

    [Fact]
    public void NoBookSelected_ShowsPrompt()
    {
        _state.SetupGet(s => s.SelectedBook).Returns((Book?)null);

        var cut = RenderPicker();

        cut.Markup.Should().Contain("Select a book first.");
    }

    [Fact]
    public void GoldenPath_RendersOneOptionPerChapter()
    {
        _state.SetupGet(s => s.SelectedBook).Returns(new Book { Usfm = "GEN", Human = "Genesis", ChapterCount = 3 });

        var cut = RenderPicker();

        var options = cut.FindAll("option");
        options.Should().HaveCount(4); // placeholder + 3 chapters
        cut.Markup.Should().Contain("Chapter 1").And.Contain("Chapter 2").And.Contain("Chapter 3");
    }

    [Fact]
    public void OnChapterChanged_ValidNumber_CallsStateSelectChapter()
    {
        _state.SetupGet(s => s.SelectedBook).Returns(new Book { Usfm = "GEN", Human = "Genesis", ChapterCount = 3 });

        var cut = RenderPicker();
        cut.Find("select").Change("2");

        _state.Verify(s => s.SelectChapter(2), Times.Once);
    }

    [Fact]
    public void OnChapterChanged_BlankSelection_DoesNotCallSelectChapter()
    {
        _state.SetupGet(s => s.SelectedBook).Returns(new Book { Usfm = "GEN", Human = "Genesis", ChapterCount = 3 });

        var cut = RenderPicker();
        cut.Find("select").Change("");

        _state.Verify(s => s.SelectChapter(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void OnStateChanged_TriggersRerender()
    {
        _state.SetupGet(s => s.SelectedBook).Returns((Book?)null);
        var cut = RenderPicker();

        _state.SetupGet(s => s.SelectedBook).Returns(new Book { Usfm = "GEN", Human = "Genesis", ChapterCount = 1 });
        _state.Raise(s => s.OnStateChanged += null);

        cut.Markup.Should().Contain("Chapter 1");
    }

    [Fact]
    public void Dispose_UnsubscribesFromStateChangedEvent()
    {
        _state.SetupGet(s => s.SelectedBook).Returns((Book?)null);
        var cut = RenderPicker();
        cut.Instance.Dispose();

        var act = () => _state.Raise(s => s.OnStateChanged += null);
        act.Should().NotThrow();
    }
}
