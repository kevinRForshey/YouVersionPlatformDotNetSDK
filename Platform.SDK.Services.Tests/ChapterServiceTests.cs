using FluentAssertions;
using Moq;
using Platform.API.Clients;
using Platform.API.Models;
using Xunit;

namespace Platform.SDK.Services.Tests;

public sealed class ChapterServiceTests
{
    [Fact]
    public async Task GetChaptersAsync_ReturnsClientResultUnchanged()
    {
        var expected = new List<Chapter>
        {
            new() { Usfm = "GEN.1", Human = "Genesis 1", VerseCount = 31 },
            new() { Usfm = "GEN.2", Human = "Genesis 2", VerseCount = 25 },
        };

        var client = new Mock<IBibleClient>();
        client.Setup(c => c.GetChaptersAsync(3034, "GEN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Chapter>)expected);

        var sut = new ChapterService(client.Object);

        var result = await sut.GetChaptersAsync(3034, "GEN");

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetChaptersAsync_ForwardsVersionIdAndBookUsfmToClient()
    {
        var client = new Mock<IBibleClient>();
        client.Setup(c => c.GetChaptersAsync(42, "JHN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Chapter>)[]);

        var sut = new ChapterService(client.Object);
        await sut.GetChaptersAsync(42, "JHN");

        client.Verify(c => c.GetChaptersAsync(42, "JHN", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetChaptersAsync_PropagatesCancellationTokenToClient()
    {
        using var cts = new CancellationTokenSource();
        var client = new Mock<IBibleClient>();
        client.Setup(c => c.GetChaptersAsync(1, "GEN", cts.Token))
            .ReturnsAsync((IReadOnlyList<Chapter>)[]);

        var sut = new ChapterService(client.Object);
        await sut.GetChaptersAsync(1, "GEN", cts.Token);

        client.Verify(c => c.GetChaptersAsync(1, "GEN", cts.Token), Times.Once);
    }
}
