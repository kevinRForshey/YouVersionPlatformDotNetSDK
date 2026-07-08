using FluentAssertions;
using Moq;
using Platform.API.Clients;
using Platform.API.Models;
using Xunit;
using YouVersion.UsfmReferences;

namespace Platform.SDK.Services.Tests;

public sealed class PassageServiceTests
{
    private static Passage MakePassage(string id) => new() { Id = id, Content = "In the beginning...", Reference = "Genesis 1:1" };

    // --- Reference overload ---

    [Fact]
    public async Task GetPassageAsync_WithReference_DelegatesDirectlyToClient()
    {
        var reference = new Reference("GEN", 1, verses: [new VerseRange(1, 1)]);
        var options = new PassageRequestOptions { Format = PassageFormat.Html };
        var expected = MakePassage("GEN.1.1");

        var client = new Mock<IPassageClient>();
        client.Setup(c => c.GetPassageAsync(3034, reference, options, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new PassageService(client.Object);

        var result = await sut.GetPassageAsync(3034, reference, options);

        result.Should().BeSameAs(expected);
        client.Verify(c => c.GetPassageAsync(3034, reference, options, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPassageAsync_WithReference_PropagatesCancellationTokenToClient()
    {
        using var cts = new CancellationTokenSource();
        var reference = new Reference("GEN", 1, verses: [new VerseRange(1, 1)]);

        var client = new Mock<IPassageClient>();
        client.Setup(c => c.GetPassageAsync(3034, reference, null, cts.Token))
            .ReturnsAsync(MakePassage("GEN.1.1"));

        var sut = new PassageService(client.Object);
        await sut.GetPassageAsync(3034, reference, cancellationToken: cts.Token);

        client.Verify(c => c.GetPassageAsync(3034, reference, null, cts.Token), Times.Once);
    }

    // --- Primitive overload ---

    [Fact]
    public async Task GetPassageAsync_FromPrimitives_SingleVerse_DefaultsVerseEndToVerseStart()
    {
        Reference? captured = null;
        var client = new Mock<IPassageClient>();
        client.Setup(c => c.GetPassageAsync(3034, It.IsAny<Reference>(), null, It.IsAny<CancellationToken>()))
            .Callback<int, Reference, PassageRequestOptions?, CancellationToken>((_, r, _, _) => captured = r)
            .ReturnsAsync(MakePassage("JHN.3.16"));

        var sut = new PassageService(client.Object);
        await sut.GetPassageAsync(3034, "JHN", 3, 16);

        captured.Should().NotBeNull();
        captured!.Book.Should().Be("JHN");
        captured.Chapter.Should().Be(3);
        captured.Verses.Should().ContainSingle().Which.Should().Be(new VerseRange(16, 16));
    }

    [Fact]
    public async Task GetPassageAsync_FromPrimitives_ExplicitVerseEnd_BuildsRange()
    {
        Reference? captured = null;
        var client = new Mock<IPassageClient>();
        client.Setup(c => c.GetPassageAsync(3034, It.IsAny<Reference>(), null, It.IsAny<CancellationToken>()))
            .Callback<int, Reference, PassageRequestOptions?, CancellationToken>((_, r, _, _) => captured = r)
            .ReturnsAsync(MakePassage("JHN.3.16-17"));

        var sut = new PassageService(client.Object);
        await sut.GetPassageAsync(3034, "JHN", 3, 16, 17);

        captured!.Verses.Should().ContainSingle().Which.Should().Be(new VerseRange(16, 17));
    }

    [Fact]
    public async Task GetPassageAsync_FromPrimitives_DelegatesToReferenceOverloadResult()
    {
        var expected = MakePassage("GEN.1.1");
        var client = new Mock<IPassageClient>();
        client.Setup(c => c.GetPassageAsync(3034, It.IsAny<Reference>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new PassageService(client.Object);
        var result = await sut.GetPassageAsync(3034, "GEN", 1, 1);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetPassageAsync_FromPrimitives_PassesOptionsAndCancellationTokenThrough()
    {
        using var cts = new CancellationTokenSource();
        var options = new PassageRequestOptions { Format = PassageFormat.Html, IncludeHeadings = true };

        var client = new Mock<IPassageClient>();
        client.Setup(c => c.GetPassageAsync(3034, It.IsAny<Reference>(), options, cts.Token))
            .ReturnsAsync(MakePassage("GEN.1.1"));

        var sut = new PassageService(client.Object);
        await sut.GetPassageAsync(3034, "GEN", 1, 1, options: options, cancellationToken: cts.Token);

        client.Verify(c => c.GetPassageAsync(3034, It.IsAny<Reference>(), options, cts.Token), Times.Once);
    }
}
