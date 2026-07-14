#region usings
using FluentAssertions;
using Moq;
using Platform.API.Clients;
using Platform.API.Models;
using Platform.SDK.Services;
using YouVersion.UsfmReferences;
using Xunit;
#endregion

namespace Platform.API.Tests.Services;

public sealed class PassageServiceTests
{
    private static readonly Passage StubPassage = new()
    {
        Id = "JHN.3.16",
        Content = "For God so loved the world...",
        Reference = "John 3:16"
    };

    // -------------------------------------------------------------------------
    // GetPassageAsync(Reference) — passthrough
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPassageAsync_WithReference_DelegatesDirectlyToClient()
    {
        var reference = Reference.FromString("JHN.3.16");
        var options = new PassageRequestOptions { Format = PassageFormat.Html };
        using var cts = new CancellationTokenSource();

        var client = new Mock<IPassageClient>();
        client.Setup(c => c.GetPassageAsync(3034, reference, options, cts.Token))
            .ReturnsAsync(StubPassage);

        var service = new PassageService(client.Object);
        var passage = await service.GetPassageAsync(3034, reference, options, cts.Token);

        passage.Should().Be(StubPassage);
        client.Verify(c => c.GetPassageAsync(3034, reference, options, cts.Token), Times.Once);
    }

    // -------------------------------------------------------------------------
    // GetPassageAsync(book, chapter, verseStart, verseEnd) — builds the Reference
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPassageAsync_FromPrimitives_BuildsSingleVerseReference_WhenVerseEndOmitted()
    {
        var client = new Mock<IPassageClient>();
        Reference? capturedReference = null;
        client.Setup(c => c.GetPassageAsync(3034, It.IsAny<Reference>(), null, default))
            .Callback<int, Reference, PassageRequestOptions?, CancellationToken>((_, r, _, _) => capturedReference = r)
            .ReturnsAsync(StubPassage);

        var service = new PassageService(client.Object);
        var passage = await service.GetPassageAsync(3034, "JHN", 3, 16);

        passage.Should().Be(StubPassage);
        capturedReference.Should().NotBeNull();
        capturedReference!.ToString().Should().Be(Reference.FromString("JHN.3.16").ToString());
    }

    [Fact]
    public async Task GetPassageAsync_FromPrimitives_BuildsVerseRangeReference_WhenVerseEndProvided()
    {
        var client = new Mock<IPassageClient>();
        Reference? capturedReference = null;
        client.Setup(c => c.GetPassageAsync(3034, It.IsAny<Reference>(), null, default))
            .Callback<int, Reference, PassageRequestOptions?, CancellationToken>((_, r, _, _) => capturedReference = r)
            .ReturnsAsync(StubPassage);

        var service = new PassageService(client.Object);
        await service.GetPassageAsync(3034, "JHN", 3, 16, 17);

        capturedReference.Should().NotBeNull();
        capturedReference!.ToString().Should().Be(Reference.FromString("JHN.3.16-17").ToString());
    }

    [Fact]
    public async Task GetPassageAsync_FromPrimitives_ForwardsOptionsAndCancellationToken()
    {
        var options = new PassageRequestOptions { Format = PassageFormat.Html, IncludeHeadings = true };
        using var cts = new CancellationTokenSource();

        var client = new Mock<IPassageClient>();
        client.Setup(c => c.GetPassageAsync(3034, It.IsAny<Reference>(), options, cts.Token))
            .ReturnsAsync(StubPassage);

        var service = new PassageService(client.Object);
        var passage = await service.GetPassageAsync(3034, "JHN", 3, 16, options: options, cancellationToken: cts.Token);

        passage.Should().Be(StubPassage);
        client.Verify(c => c.GetPassageAsync(3034, It.IsAny<Reference>(), options, cts.Token), Times.Once);
    }
}
