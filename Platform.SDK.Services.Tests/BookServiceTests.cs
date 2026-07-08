using FluentAssertions;
using Moq;
using Platform.API.Clients;
using Platform.API.Models;
using Xunit;

namespace Platform.SDK.Services.Tests;

public sealed class BookServiceTests
{
    [Fact]
    public async Task GetBooksAsync_ReturnsClientResultUnchanged()
    {
        var expected = new List<Book>
        {
            new() { Usfm = "GEN", Human = "Genesis", ChapterCount = 50 },
            new() { Usfm = "EXO", Human = "Exodus", ChapterCount = 40 },
        };

        var client = new Mock<IBibleClient>();
        client.Setup(c => c.GetBooksAsync(3034, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Book>)expected);

        var sut = new BookService(client.Object);

        var result = await sut.GetBooksAsync(3034);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetBooksAsync_ForwardsVersionIdToClient()
    {
        var client = new Mock<IBibleClient>();
        client.Setup(c => c.GetBooksAsync(1234, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Book>)[]);

        var sut = new BookService(client.Object);
        await sut.GetBooksAsync(1234);

        client.Verify(c => c.GetBooksAsync(1234, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBooksAsync_PropagatesCancellationTokenToClient()
    {
        using var cts = new CancellationTokenSource();
        var client = new Mock<IBibleClient>();
        client.Setup(c => c.GetBooksAsync(1, cts.Token))
            .ReturnsAsync((IReadOnlyList<Book>)[]);

        var sut = new BookService(client.Object);
        await sut.GetBooksAsync(1, cts.Token);

        client.Verify(c => c.GetBooksAsync(1, cts.Token), Times.Once);
    }
}
