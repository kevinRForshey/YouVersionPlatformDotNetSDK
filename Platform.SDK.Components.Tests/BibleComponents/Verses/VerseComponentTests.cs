using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Platform.API.Models;
using Platform.API.OAuth;
using Platform.SDK.Components.BibleComponents.Verses;
using Platform.SDK.Services;
using Xunit;
using YouVersion.UsfmReferences;

namespace Platform.SDK.Components.Tests.BibleComponents.Verses;

public sealed class VerseComponentTests : TestContext
{
    private const string MultiVerseContent =
        "<div><div class=\"p\">" +
        "<span class=\"yv-v\" v=\"16\"></span><span class=\"yv-vlbl\">16</span>For God so loved the world. " +
        "<span class=\"yv-v\" v=\"17\"></span><span class=\"yv-vlbl\">17</span>For God did not send his Son. " +
        "</div></div>";

    private static Passage MakePassage() => new()
    {
        Id = "JHN.3.16",
        Content = "For God so loved the world...",
        Reference = "John 3:16",
    };

    private static Passage MakeMultiVersePassage() => new()
    {
        Id = "JHN.3.16-17",
        Reference = "John 3:16-17",
        Content = MultiVerseContent,
    };

    private static Highlight MakeHighlight(string passageId, string color) => new()
    {
        BibleId = 3034,
        PassageId = passageId,
        Color = color,
    };

    private static OAuthTokenResponse MakeValidToken() => new()
    {
        AccessToken = "valid-access-token",
        ExpiresIn = 3600,
        ReceivedAt = DateTimeOffset.UtcNow,
    };

    private readonly Mock<IHighlightService> _highlightService = new();
    private readonly Mock<ITokenProvider> _tokenProvider = new();

    public VerseComponentTests()
    {
        _highlightService
            .Setup(s => s.GetHighlightsAsync(It.IsAny<int>(), It.IsAny<Reference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Highlight>)[]);

        // Signed in by default so existing highlighting tests exercise the interactive toolbar;
        // sign-out behavior is covered by its own tests below.
        _tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(MakeValidToken());
    }

    private void RegisterServices()
    {
        Services.AddSingleton(_highlightService.Object);
        Services.AddSingleton(_tokenProvider.Object);
    }

    private IRenderedComponent<VerseComponent> RenderVerse(
        int versionId = 3034,
        bool enableHighlighting = true,
        EventCallback<Highlight>? onHighlightCreated = null,
        EventCallback<Highlight>? onHighlightCleared = null)
    {
        RegisterServices();

        return RenderComponent<VerseComponent>(p =>
        {
            p.Add(x => x.Passage, MakeMultiVersePassage());
            p.Add(x => x.VersionId, versionId);
            p.Add(x => x.EnableHighlighting, enableHighlighting);
            if (onHighlightCreated is { } createdCallback)
                p.Add(x => x.OnHighlightCreated, createdCallback);
            if (onHighlightCleared is { } clearedCallback)
                p.Add(x => x.OnHighlightCleared, clearedCallback);
        });
    }

    [Fact]
    public void GoldenPath_RendersReferenceAndContent()
    {
        RegisterServices();
        var cut = RenderComponent<VerseComponent>(p => p.Add(x => x.Passage, MakePassage()));

        cut.Markup.Should().Contain("John 3:16");
        cut.Markup.Should().Contain("For God so loved the world");
    }

    [Fact]
    public void GoldenPath_ParsesContentIntoPerVerseSegments()
    {
        var cut = RenderVerse();

        cut.FindAll(".verse-segment").Should().HaveCount(2);
        cut.Markup.Should().Contain("For God so loved the world");
        cut.Markup.Should().Contain("For God did not send his Son");
    }

    [Fact]
    public void NoVersionId_HidesHighlightToolbarAndStillRendersVerses()
    {
        var cut = RenderVerse(versionId: 0);

        cut.FindAll(".highlight-toolbar").Should().BeEmpty();
        cut.FindAll(".verse-segment").Should().HaveCount(2);
    }

    [Fact]
    public void HighlightingDisabled_HidesToolbarAndSkipsSignInCheckAndHighlightLoad()
    {
        var cut = RenderVerse(enableHighlighting: false);

        cut.FindAll(".highlight-toolbar").Should().BeEmpty();
        cut.FindAll(".verse-segment").Should().HaveCount(2);
        _tokenProvider.Verify(t => t.GetTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
        _highlightService.Verify(
            s => s.GetHighlightsAsync(It.IsAny<int>(), It.IsAny<Reference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SignedOut_ShowsSignInPromptInsteadOfSwatches()
    {
        _tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((OAuthTokenResponse?)null);

        var cut = RenderVerse();

        cut.FindAll(".highlight-swatch").Should().BeEmpty();
        cut.Markup.Should().Contain("Sign in to highlight verses");
    }

    [Fact]
    public void SignedOut_DoesNotLoadHighlights()
    {
        _tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((OAuthTokenResponse?)null);

        RenderVerse();

        _highlightService.Verify(
            s => s.GetHighlightsAsync(It.IsAny<int>(), It.IsAny<Reference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ArmColorThenClickVerse_CreatesHighlightAndInvokesCallback()
    {
        var saved = MakeHighlight("JHN.3.16", "ffd54f");
        _highlightService
            .Setup(s => s.CreateOrUpdateHighlightAsync(3034, It.Is<Reference>(r => r.ToString() == "JHN.3.16"), "ffd54f", It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);

        Highlight? received = null;
        var cut = RenderVerse(onHighlightCreated: EventCallback.Factory.Create<Highlight>(this, h => received = h));

        cut.Find("button[title='Yellow']").Click();
        cut.FindAll(".verse-segment")[0].Click();

        received.Should().Be(saved);
        cut.Find(".verse-segment--highlighted").GetAttribute("style").Should().Contain("ffd54f");
        _highlightService.Verify(
            s => s.CreateOrUpdateHighlightAsync(3034, It.Is<Reference>(r => r.ToString() == "JHN.3.16"), "ffd54f", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ClickVerseWithoutArmedColor_IsNoOp()
    {
        var cut = RenderVerse();

        cut.FindAll(".verse-segment")[0].Click();

        cut.FindAll(".verse-segment--highlighted").Should().BeEmpty();
        _highlightService.Verify(
            s => s.CreateOrUpdateHighlightAsync(It.IsAny<int>(), It.IsAny<Reference>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ClickAlreadyHighlightedVerse_IsNoOp()
    {
        _highlightService
            .Setup(s => s.GetHighlightsAsync(It.IsAny<int>(), It.IsAny<Reference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Highlight>)[MakeHighlight("JHN.3.16", "81c784")]);

        var cut = RenderVerse();
        cut.Find("button[title='Yellow']").Click();
        cut.FindAll(".verse-segment")[0].Click();

        cut.Find(".verse-segment--highlighted").GetAttribute("style").Should().Contain("81c784");
        _highlightService.Verify(
            s => s.CreateOrUpdateHighlightAsync(It.IsAny<int>(), It.IsAny<Reference>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ExistingHighlights_RenderWithCorrectBackgroundOnLoad()
    {
        _highlightService
            .Setup(s => s.GetHighlightsAsync(It.IsAny<int>(), It.IsAny<Reference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Highlight>)[MakeHighlight("JHN.3.17", "64b5f6")]);

        var cut = RenderVerse();

        cut.FindAll(".verse-segment--highlighted").Should().HaveCount(1);
        cut.Find(".verse-segment--highlighted").GetAttribute("style").Should().Contain("64b5f6");
    }

    [Fact]
    public void DoubleClickHighlightedVerse_ClearsHighlightAndInvokesCallback()
    {
        var existing = MakeHighlight("JHN.3.16", "ffd54f");
        _highlightService
            .Setup(s => s.GetHighlightsAsync(It.IsAny<int>(), It.IsAny<Reference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Highlight>)[existing]);
        _highlightService
            .Setup(s => s.ClearHighlightsAsync(3034, It.Is<Reference>(r => r.ToString() == "JHN.3.16"), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Highlight? cleared = null;
        var cut = RenderVerse(onHighlightCleared: EventCallback.Factory.Create<Highlight>(this, h => cleared = h));

        cut.FindAll(".verse-segment")[0].DoubleClick();

        cleared.Should().Be(existing);
        cut.FindAll(".verse-segment--highlighted").Should().BeEmpty();
        _highlightService.Verify(
            s => s.ClearHighlightsAsync(3034, It.Is<Reference>(r => r.ToString() == "JHN.3.16"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void DoubleClickUnhighlightedVerse_IsNoOp()
    {
        var cut = RenderVerse();

        cut.FindAll(".verse-segment")[0].DoubleClick();

        _highlightService.Verify(
            s => s.ClearHighlightsAsync(It.IsAny<int>(), It.IsAny<Reference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void WhenCreateThrows_ShowsErrorMessage()
    {
        _highlightService
            .Setup(s => s.CreateOrUpdateHighlightAsync(3034, It.IsAny<Reference>(), "ffd54f", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderVerse();
        cut.Find("button[title='Yellow']").Click();
        cut.FindAll(".verse-segment")[0].Click();

        cut.Markup.Should().Contain("Could not save highlight").And.Contain("boom");
    }

    [Fact]
    public void WhenCreateThrowsUnauthorized_ShowsSignInMessage()
    {
        _highlightService
            .Setup(s => s.CreateOrUpdateHighlightAsync(3034, It.IsAny<Reference>(), "ffd54f", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Platform.API.Exceptions.YouVersionApiException(
                System.Net.HttpStatusCode.Unauthorized, "failed", null));

        var cut = RenderVerse();
        cut.Find("button[title='Yellow']").Click();
        cut.FindAll(".verse-segment")[0].Click();

        cut.Markup.Should().Contain("Highlights access isn't available");
    }

    [Fact]
    public void WhenClearThrows_ShowsErrorMessage()
    {
        _highlightService
            .Setup(s => s.GetHighlightsAsync(It.IsAny<int>(), It.IsAny<Reference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Highlight>)[MakeHighlight("JHN.3.16", "ffd54f")]);
        _highlightService
            .Setup(s => s.ClearHighlightsAsync(3034, It.IsAny<Reference>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = RenderVerse();
        cut.FindAll(".verse-segment")[0].DoubleClick();

        cut.Markup.Should().Contain("Could not remove highlight").And.Contain("boom");
    }

    [Fact]
    public void WhileSaving_ShowsSavingIndicator()
    {
        var tcs = new TaskCompletionSource<Highlight>();
        _highlightService
            .Setup(s => s.CreateOrUpdateHighlightAsync(3034, It.IsAny<Reference>(), "ffd54f", It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var cut = RenderVerse();
        cut.Find("button[title='Yellow']").Click();
        cut.FindAll(".verse-segment")[0].Click();

        cut.Markup.Should().Contain("verse-segment-saving");

        tcs.SetResult(MakeHighlight("JHN.3.16", "ffd54f"));
    }

    [Fact]
    public void WithCopyright_RendersCopyrightFooter()
    {
        RegisterServices();
        var cut = RenderComponent<VerseComponent>(p => p
            .Add(x => x.Passage, MakePassage())
            .Add(x => x.Copyright, "© 2011 Some Publisher"));

        cut.Markup.Should().Contain("verse-footer");
        cut.Markup.Should().Contain("2011 Some Publisher");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithoutCopyright_OmitsCopyrightFooter(string? copyright)
    {
        RegisterServices();
        var cut = RenderComponent<VerseComponent>(p => p
            .Add(x => x.Passage, MakePassage())
            .Add(x => x.Copyright, copyright));

        cut.Markup.Should().NotContain("verse-footer");
    }

    [Fact]
    public void ContentIsRenderedAsMarkup_NotEscaped()
    {
        RegisterServices();
        var passage = MakePassage() with { Content = "<p>Hello <strong>world</strong></p>" };

        var cut = RenderComponent<VerseComponent>(p => p.Add(x => x.Passage, passage));

        cut.Find("strong").TextContent.Should().Be("world");
    }
}
