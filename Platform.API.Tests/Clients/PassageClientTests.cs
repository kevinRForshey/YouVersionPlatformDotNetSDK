#region usings 
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.API.Clients;
using Platform.API.Exceptions;
using Platform.API.Models;
using Platform.API.Tests.Fakes;
using YouVersion.UsfmReferences;
using Xunit;
#endregion

namespace Platform.API.Tests.Clients;

public sealed class PassageClientTests
{
    private const string PassageJson = """
        { "id": "JHN.3.16", "content": "For God so loved the world...", "reference": "John 3:16" }
        """;

    // -------------------------------------------------------------------------
    // GetPassageAsync — success paths
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPassageAsync_ReturnsPassage_WhenApiSucceeds()
    {
        var client = BuildClient(HttpStatusCode.OK, PassageJson);

        var reference = Reference.FromString("JHN.3.16");
        var passage = await client.GetPassageAsync(3034, reference);

        passage.Id.Should().Be("JHN.3.16");
        passage.Content.Should().Contain("God so loved");
        passage.Reference.Should().Be("John 3:16");
    }

    [Fact]
    public async Task GetPassageAsync_UsesTextFormat_ByDefault()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, PassageJson);
        var client = BuildClientFromHandler(handler);

        await client.GetPassageAsync(3034, TestReferences.John316);

        handler.LastRequest!.RequestUri!.Query.Should().Contain("format=text");
    }

    [Fact]
    public async Task GetPassageAsync_UsesHtmlFormat_WhenRequested()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, PassageJson);
        var client = BuildClientFromHandler(handler);

        await client.GetPassageAsync(3034, TestReferences.John316,
            new PassageRequestOptions { Format = PassageFormat.Html });

        handler.LastRequest!.RequestUri!.Query.Should().Contain("format=html");
    }

    [Fact]
    public async Task GetPassageAsync_IncludesHeadings_WhenRequested()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, PassageJson);
        var client = BuildClientFromHandler(handler);

        await client.GetPassageAsync(3034, TestReferences.Genesis1,
            new PassageRequestOptions { Format = PassageFormat.Html, IncludeHeadings = true });

        handler.LastRequest!.RequestUri!.Query.Should().Contain("include_headings=true");
    }

    [Fact]
    public async Task GetPassageAsync_IncludesNotes_WhenRequested()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, PassageJson);
        var client = BuildClientFromHandler(handler);

        await client.GetPassageAsync(3034, TestReferences.Genesis1,
            new PassageRequestOptions { Format = PassageFormat.Html, IncludeNotes = true });

        handler.LastRequest!.RequestUri!.Query.Should().Contain("include_notes=true");
    }

    [Fact]
    public async Task GetPassageAsync_EncodesUsfmInUrl()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, PassageJson);
        var client = BuildClientFromHandler(handler);

        await client.GetPassageAsync(3034, TestReferences.John316To17);

        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Contain("JHN.3.16-17");
    }

    [Fact]
    public async Task GetPassageAsync_ThrowsArgumentOutOfRangeException_WhenVersionIdIsNotPositive()
    {
        var client = BuildClient(HttpStatusCode.OK, PassageJson);
        var act = () => client.GetPassageAsync(0, Reference.FromString("JHN.3.16"), null);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetPassageAsync_ThrowsArgumentException_WhenUsfmIsInvalid(string usfm)
    {
        var client = BuildClient(HttpStatusCode.OK, PassageJson);
        var act = () => client.GetPassageAsync(3034, Reference.FromString(usfm));
        await act.Should().ThrowAsync<FormatException>();
    }

    // -------------------------------------------------------------------------
    // GetPassageAsync — error paths
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetPassageAsync_ThrowsYouVersionApiException_WhenApiReturnsError(HttpStatusCode status)
    {
        var client = BuildClient(status, """{"error":"fail"}""");
        var act = () => client.GetPassageAsync(3034, TestReferences.John316);
        await act.Should().ThrowAsync<YouVersionApiException>()
            .Where(e => e.StatusCode == status);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static PassageClient BuildClient(HttpStatusCode status, string json)
        => BuildClientFromHandler(new FakeHttpMessageHandler(status, json));

    private static PassageClient BuildClientFromHandler(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.youversion.com") };
        var usfmService = new UsfmReferenceService();
        return new PassageClient(httpClient, NullLogger<PassageClient>.Instance, usfmService);
    }
}
