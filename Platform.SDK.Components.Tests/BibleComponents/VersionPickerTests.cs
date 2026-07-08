using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Platform.API.Models;
using Platform.SDK.Components.BibleComponents;
using Platform.SDK.Services;
using Xunit;

namespace Platform.SDK.Components.Tests.BibleComponents;

public sealed class VersionPickerTests : TestContext
{
    private static BibleVersionSummary MakeVersion(int id, string abbreviation) => new()
    {
        Id = id,
        Abbreviation = abbreviation,
        LocalizedAbbreviation = abbreviation,
        Title = abbreviation,
        LocalizedTitle = abbreviation,
        LanguageTag = "en",
    };

    private Mock<IVersionService> _versionService = new();
    private Mock<IBibleReaderStateService> _state = new();

    private IRenderedComponent<VersionPicker> RenderPicker(string? languageRange = null)
    {
        Services.AddSingleton(_versionService.Object);
        Services.AddSingleton(_state.Object);

        return languageRange is null
            ? RenderComponent<VersionPicker>()
            : RenderComponent<VersionPicker>(p => p.Add(x => x.LanguageRange, languageRange));
    }

    [Fact]
    public void GoldenPath_LoadsAndRendersVersionsFromService()
    {
        _versionService
            .Setup(s => s.GetVersionsAsync("en", It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeVersion(1, "NIV"), MakeVersion(2, "BSB")]);

        var cut = RenderPicker();

        var options = cut.FindAll("option");
        options.Should().HaveCount(3); // placeholder + 2 versions
        cut.Markup.Should().Contain("NIV").And.Contain("BSB");
    }

    [Fact]
    public void WhenServiceThrows_ShowsErrorMessage()
    {
        _versionService
            .Setup(s => s.GetVersionsAsync("en", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderPicker();

        cut.Markup.Should().Contain("Could not load Bible versions").And.Contain("boom");
    }

    [Fact]
    public void OnParametersSet_SameLanguageRange_DoesNotReloadVersions()
    {
        _versionService
            .Setup(s => s.GetVersionsAsync("en", It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeVersion(1, "NIV")]);

        var cut = RenderPicker();
        cut.SetParametersAndRender(p => p.Add(x => x.LanguageRange, "en"));

        _versionService.Verify(s => s.GetVersionsAsync("en", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnParametersSet_LanguageRangeChanges_ReloadsVersions()
    {
        _versionService
            .Setup(s => s.GetVersionsAsync("en", It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeVersion(1, "NIV")]);
        _versionService
            .Setup(s => s.GetVersionsAsync("es", It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeVersion(2, "RVR")]);

        var cut = RenderPicker();
        cut.SetParametersAndRender(p => p.Add(x => x.LanguageRange, "es"));

        _versionService.Verify(s => s.GetVersionsAsync("en", It.IsAny<CancellationToken>()), Times.Once);
        _versionService.Verify(s => s.GetVersionsAsync("es", It.IsAny<CancellationToken>()), Times.Once);
        cut.Markup.Should().Contain("RVR");
    }

    [Fact]
    public void SelectingAVersion_CallsStateSelectVersion()
    {
        var niv = MakeVersion(1, "NIV");
        _versionService
            .Setup(s => s.GetVersionsAsync("en", It.IsAny<CancellationToken>()))
            .ReturnsAsync([niv]);

        var cut = RenderPicker();
        cut.Find("select").Change("1");

        _state.Verify(s => s.SelectVersion(niv), Times.Once);
    }

    [Fact]
    public void SelectingTheBlankOption_ResetsState()
    {
        _versionService
            .Setup(s => s.GetVersionsAsync("en", It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeVersion(1, "NIV")]);

        var cut = RenderPicker();
        cut.Find("select").Change("");

        _state.Verify(s => s.Reset(), Times.Once);
    }
}
