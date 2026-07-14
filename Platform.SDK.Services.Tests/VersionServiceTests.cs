using FluentAssertions;
using Moq;
using Platform.API.Clients;
using Platform.API.Models;
using Xunit;

namespace Platform.SDK.Services.Tests;

public sealed class VersionServiceTests
{
    private static BibleVersionSummary MakeVersion(int id) => new()
    {
        Id = id,
        Abbreviation = $"V{id}",
        LocalizedAbbreviation = $"V{id}",
        Title = $"Version {id}",
        LocalizedTitle = $"Version {id}",
        LanguageTag = "en",
    };

    [Fact]
    public async Task GetVersionsAsync_SinglePage_ReturnsAllItemsAndStops()
    {
        var client = new Mock<IBibleClient>();
        client.Setup(c => c.GetVersionsAsync("en", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<BibleVersionSummary>
            {
                Data = [MakeVersion(1), MakeVersion(2)],
                NextPageToken = null,
            });

        var sut = new VersionService(client.Object);

        var result = await sut.GetVersionsAsync();

        result.Should().HaveCount(2);
        result.Select(v => v.Id).Should().Equal(1, 2);
        client.Verify(c => c.GetVersionsAsync("en", null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetVersionsAsync_MultiplePages_AccumulatesAllItemsAcrossPages()
    {
        var client = new Mock<IBibleClient>();
        client.SetupSequence(c => c.GetVersionsAsync("en", It.IsAny<string?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<BibleVersionSummary> { Data = [MakeVersion(1)], NextPageToken = "page2" })
            .ReturnsAsync(new PagedResult<BibleVersionSummary> { Data = [MakeVersion(2)], NextPageToken = "page3" })
            .ReturnsAsync(new PagedResult<BibleVersionSummary> { Data = [MakeVersion(3)], NextPageToken = null });

        var sut = new VersionService(client.Object);

        var result = await sut.GetVersionsAsync();

        result.Select(v => v.Id).Should().Equal(1, 2, 3);
        client.Verify(
            c => c.GetVersionsAsync("en", It.IsAny<string?>(), null, It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task GetVersionsAsync_FollowsPageTokenChainInOrder()
    {
        var client = new Mock<IBibleClient>();
        var seenTokens = new List<string?>();

        client.Setup(c => c.GetVersionsAsync("en", It.IsAny<string?>(), null, It.IsAny<CancellationToken>()))
            .Callback<string, string?, int?, CancellationToken>((_, token, _, _) => seenTokens.Add(token))
            .ReturnsAsync((string _, string? token, int? _, CancellationToken _) => token switch
            {
                null => new PagedResult<BibleVersionSummary> { Data = [MakeVersion(1)], NextPageToken = "abc" },
                "abc" => new PagedResult<BibleVersionSummary> { Data = [MakeVersion(2)], NextPageToken = null },
                _ => throw new InvalidOperationException("Unexpected page token"),
            });

        var sut = new VersionService(client.Object);
        await sut.GetVersionsAsync();

        seenTokens.Should().Equal(new string?[] { null, "abc" });
    }

    [Fact]
    public async Task GetVersionsAsync_EmptyResult_ReturnsEmptyList()
    {
        var client = new Mock<IBibleClient>();
        client.Setup(c => c.GetVersionsAsync("en", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<BibleVersionSummary> { Data = [], NextPageToken = null });

        var sut = new VersionService(client.Object);

        var result = await sut.GetVersionsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVersionsAsync_EmptyStringPageToken_TreatedAsNoMorePages()
    {
        var client = new Mock<IBibleClient>();
        client.Setup(c => c.GetVersionsAsync("en", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<BibleVersionSummary> { Data = [MakeVersion(1)], NextPageToken = "" });

        var sut = new VersionService(client.Object);

        var result = await sut.GetVersionsAsync();

        result.Should().HaveCount(1);
        client.Verify(c => c.GetVersionsAsync("en", null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetVersionsAsync_CustomLanguageRange_IsPassedToClient()
    {
        var client = new Mock<IBibleClient>();
        client.Setup(c => c.GetVersionsAsync("es", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<BibleVersionSummary> { Data = [MakeVersion(1)], NextPageToken = null });

        var sut = new VersionService(client.Object);
        await sut.GetVersionsAsync("es");

        client.Verify(c => c.GetVersionsAsync("es", null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetVersionsAsync_PropagatesCancellationTokenToClient()
    {
        using var cts = new CancellationTokenSource();
        var client = new Mock<IBibleClient>();
        client.Setup(c => c.GetVersionsAsync("en", null, null, cts.Token))
            .ReturnsAsync(new PagedResult<BibleVersionSummary> { Data = [], NextPageToken = null });

        var sut = new VersionService(client.Object);
        await sut.GetVersionsAsync(cancellationToken: cts.Token);

        client.Verify(c => c.GetVersionsAsync("en", null, null, cts.Token), Times.Once);
    }
}
