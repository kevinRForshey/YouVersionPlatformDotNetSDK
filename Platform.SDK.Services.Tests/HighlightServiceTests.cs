using System.Net;
using FluentAssertions;
using Moq;
using Platform.API.Clients;
using Platform.API.Exceptions;
using Platform.API.Models;
using Xunit;
using YouVersion.UsfmReferences;

namespace Platform.SDK.Services.Tests;

public sealed class HighlightServiceTests
{
    private static readonly Reference John316 = Reference.FromString("JHN.3.16");
    private static readonly Reference Chapter3 = Reference.FromString("JHN.3");

    private static Highlight MakeHighlight(string passageId = "JHN.3.16", int bibleId = 3034, string color = "44aa44") => new()
    {
        BibleId = bibleId,
        PassageId = passageId,
        Color = color,
    };

    // -------------------------------------------------------------------
    // GetHighlightsAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetHighlightsAsync_DelegatesToClientAndReturnsResult()
    {
        IReadOnlyList<Highlight> highlights = [MakeHighlight("JHN.3.16"), MakeHighlight("JHN.3.17")];
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.GetHighlightsAsync(3034, Chapter3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(highlights);

        var sut = new HighlightService(client.Object);

        var result = await sut.GetHighlightsAsync(3034, Chapter3);

        result.Should().BeEquivalentTo(highlights);
        client.Verify(c => c.GetHighlightsAsync(3034, Chapter3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetHighlightsAsync_PropagatesCancellationTokenToClient()
    {
        using var cts = new CancellationTokenSource();
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.GetHighlightsAsync(3034, Chapter3, cts.Token))
            .ReturnsAsync((IReadOnlyList<Highlight>)[]);

        var sut = new HighlightService(client.Object);
        await sut.GetHighlightsAsync(3034, Chapter3, cts.Token);

        client.Verify(c => c.GetHighlightsAsync(3034, Chapter3, cts.Token), Times.Once);
    }

    [Fact]
    public async Task GetHighlightsAsync_PropagatesExceptionFromClient()
    {
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.GetHighlightsAsync(0, Chapter3, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentOutOfRangeException("bibleId"));

        var sut = new HighlightService(client.Object);

        var act = () => sut.GetHighlightsAsync(0, Chapter3);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetHighlightsAsync_WhenClientThrowsUnauthorized_ThrowsHighlightAccessDeniedException()
    {
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.GetHighlightsAsync(3034, Chapter3, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new YouVersionApiException(HttpStatusCode.Unauthorized, "unauthorized", null));

        var sut = new HighlightService(client.Object);

        var act = () => sut.GetHighlightsAsync(3034, Chapter3);

        await act.Should().ThrowAsync<HighlightAccessDeniedException>();
    }

    // -------------------------------------------------------------------
    // GetRecentColorsAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetRecentColorsAsync_DelegatesToClientAndReturnsResult()
    {
        IReadOnlyList<string> colors = ["44aa44", "ffd54f"];
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.GetRecentColorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(colors);

        var sut = new HighlightService(client.Object);

        var result = await sut.GetRecentColorsAsync();

        result.Should().Equal(colors);
        client.Verify(c => c.GetRecentColorsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------
    // CreateOrUpdateHighlightAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task CreateOrUpdateHighlightAsync_DelegatesToClientAndReturnsResult()
    {
        var saved = MakeHighlight();
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.CreateOrUpdateHighlightAsync(3034, John316, "44aa44", It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);

        var sut = new HighlightService(client.Object);

        var result = await sut.CreateOrUpdateHighlightAsync(3034, John316, "44aa44");

        result.Should().Be(saved);
        client.Verify(
            c => c.CreateOrUpdateHighlightAsync(3034, John316, "44aa44", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdateHighlightAsync_PropagatesCancellationTokenToClient()
    {
        using var cts = new CancellationTokenSource();
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.CreateOrUpdateHighlightAsync(3034, John316, "44aa44", cts.Token))
            .ReturnsAsync(MakeHighlight());

        var sut = new HighlightService(client.Object);
        await sut.CreateOrUpdateHighlightAsync(3034, John316, "44aa44", cts.Token);

        client.Verify(c => c.CreateOrUpdateHighlightAsync(3034, John316, "44aa44", cts.Token), Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdateHighlightAsync_PropagatesExceptionFromClient()
    {
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.CreateOrUpdateHighlightAsync(0, John316, "44aa44", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentOutOfRangeException("bibleId"));

        var sut = new HighlightService(client.Object);

        var act = () => sut.CreateOrUpdateHighlightAsync(0, John316, "44aa44");

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CreateOrUpdateHighlightAsync_WhenClientThrowsUnauthorized_ThrowsHighlightAccessDeniedException()
    {
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.CreateOrUpdateHighlightAsync(3034, John316, "44aa44", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new YouVersionApiException(HttpStatusCode.Unauthorized, "unauthorized", null));

        var sut = new HighlightService(client.Object);

        var act = () => sut.CreateOrUpdateHighlightAsync(3034, John316, "44aa44");

        await act.Should().ThrowAsync<HighlightAccessDeniedException>();
    }

    // -------------------------------------------------------------------
    // ClearHighlightsAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task ClearHighlightsAsync_DelegatesToClient()
    {
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.ClearHighlightsAsync(3034, John316, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new HighlightService(client.Object);
        await sut.ClearHighlightsAsync(3034, John316);

        client.Verify(c => c.ClearHighlightsAsync(3034, John316, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearHighlightsAsync_PropagatesCancellationTokenToClient()
    {
        using var cts = new CancellationTokenSource();
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.ClearHighlightsAsync(3034, John316, cts.Token))
            .Returns(Task.CompletedTask);

        var sut = new HighlightService(client.Object);
        await sut.ClearHighlightsAsync(3034, John316, cts.Token);

        client.Verify(c => c.ClearHighlightsAsync(3034, John316, cts.Token), Times.Once);
    }

    [Fact]
    public async Task ClearHighlightsAsync_PropagatesExceptionFromClient()
    {
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.ClearHighlightsAsync(3034, John316, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Passage is required."));

        var sut = new HighlightService(client.Object);

        var act = () => sut.ClearHighlightsAsync(3034, John316);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ClearHighlightsAsync_WhenClientThrowsUnauthorized_ThrowsHighlightAccessDeniedException()
    {
        var client = new Mock<IHighlightClient>();
        client.Setup(c => c.ClearHighlightsAsync(3034, John316, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new YouVersionApiException(HttpStatusCode.Unauthorized, "unauthorized", null));

        var sut = new HighlightService(client.Object);

        var act = () => sut.ClearHighlightsAsync(3034, John316);

        await act.Should().ThrowAsync<HighlightAccessDeniedException>();
    }
}
