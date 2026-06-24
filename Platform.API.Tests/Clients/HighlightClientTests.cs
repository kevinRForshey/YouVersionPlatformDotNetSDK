using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.API.Clients;
using Platform.API.Exceptions;
using Platform.API.Models;
using Platform.API.Tests.Fakes;
using YouVersion.UsfmReferences;
using Xunit;

namespace Platform.API.Tests.Clients;

public sealed class HighlightClientTests
{
    private const string HighlightJson = """
        {
          "id": "hl-1", "usfm": "JHN.3.16", "version_id": 3034,
          "color": "Yellow", "created_at": "2024-01-01T00:00:00Z", "updated_at": "2024-01-01T00:00:00Z"
        }
        """;

    private const string PagedHighlightsJson = """
        {
          "data": [
            { "id": "hl-1", "usfm": "JHN.3.16", "version_id": 3034,
              "color": "Yellow", "created_at": "2024-01-01T00:00:00Z", "updated_at": "2024-01-01T00:00:00Z" }
          ],
          "next_page_token": null
        }
        """;

    // -------------------------------------------------------------------------
    // GetHighlightsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetHighlightsAsync_ReturnsHighlights_WhenApiSucceeds()
    {
        var client = BuildClient(HttpStatusCode.OK, PagedHighlightsJson);

        var result = await client.GetHighlightsAsync();

        result.Data.Should().HaveCount(1);
        result.Data[0].Id.Should().Be("hl-1");
        result.Data[0].Usfm.Should().Be("JHN.3.16");
    }

    [Fact]
    public async Task GetHighlightsAsync_IncludesPageToken_WhenProvided()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, PagedHighlightsJson);
        var client = BuildClientFromHandler(handler);

        await client.GetHighlightsAsync(pageToken: "tok456");

        handler.LastRequest!.RequestUri!.Query.Should().Contain("page_token=tok456");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetHighlightsAsync_ThrowsArgumentException_WhenPageTokenIsInvalid(string pageToken)
    {
        var client = BuildClient(HttpStatusCode.OK, PagedHighlightsJson);
        var act = () => client.GetHighlightsAsync(pageToken);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetHighlightsAsync_ThrowsYouVersionApiException_OnError(HttpStatusCode status)
    {
        var client = BuildClient(status, """{"error":"fail"}""");
        var act = () => client.GetHighlightsAsync();
        await act.Should().ThrowAsync<YouVersionApiException>()
            .Where(e => e.StatusCode == status);
    }

    // -------------------------------------------------------------------------
    // CreateHighlightAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateHighlightAsync_ReturnsCreatedHighlight_WhenApiSucceeds()
    {
        var client = BuildClient(HttpStatusCode.Created, HighlightJson);

        var highlight = await client.CreateHighlightAsync(3034, TestReferences.John316, HighlightColor.Yellow);

        highlight.Id.Should().Be("hl-1");
        highlight.Usfm.Should().Be("JHN.3.16");
        highlight.VersionId.Should().Be(3034);
    }

    [Fact]
    public async Task CreateHighlightAsync_SendsPostRequest()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Created, HighlightJson);
        var client = BuildClientFromHandler(handler);

        await client.CreateHighlightAsync(3034, TestReferences.John316, HighlightColor.Blue);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task CreateHighlightAsync_ThrowsYouVersionApiException_OnError()
    {
        var client = BuildClient(HttpStatusCode.Unauthorized, """{"error":"unauthorized"}""");
        var act = () => client.CreateHighlightAsync(3034, TestReferences.John316, HighlightColor.Yellow);
        await act.Should().ThrowAsync<YouVersionApiException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateHighlightAsync_ThrowsArgumentOutOfRangeException_WhenVersionIdIsNotPositive()
    {
        var client = BuildClient(HttpStatusCode.Created, HighlightJson);
        var act = () => client.CreateHighlightAsync(0, TestReferences.John316, HighlightColor.Yellow);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CreateHighlightAsync_ThrowsArgumentOutOfRangeException_WhenColorIsInvalid()
    {
        var client = BuildClient(HttpStatusCode.Created, HighlightJson);
        var invalidColor = (HighlightColor)999;
        var act = () => client.CreateHighlightAsync(3034, TestReferences.John316, invalidColor);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // -------------------------------------------------------------------------
    // DeleteHighlightAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteHighlightAsync_Succeeds_WhenApiReturnsNoContent()
    {
        var client = BuildClient(HttpStatusCode.NoContent, string.Empty);
        var act = () => client.DeleteHighlightAsync("hl-1");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteHighlightAsync_SendsDeleteRequest()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var client = BuildClientFromHandler(handler);

        await client.DeleteHighlightAsync("hl-1");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().EndWith("hl-1");
    }

    [Fact]
    public async Task DeleteHighlightAsync_ThrowsYouVersionApiException_WhenNotFound()
    {
        var client = BuildClient(HttpStatusCode.NotFound, """{"error":"not found"}""");
        var act = () => client.DeleteHighlightAsync("hl-missing");
        await act.Should().ThrowAsync<YouVersionApiException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DeleteHighlightAsync_ThrowsArgumentException_WhenHighlightIdIsInvalid(string highlightId)
    {
        var client = BuildClient(HttpStatusCode.NoContent, string.Empty);
        var act = () => client.DeleteHighlightAsync(highlightId);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static HighlightClient BuildClient(HttpStatusCode status, string json)
        => BuildClientFromHandler(new FakeHttpMessageHandler(status, json));

    private static HighlightClient BuildClientFromHandler(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.youversion.com") };
        var usfmService = new UsfmReferenceService();
        return new HighlightClient(httpClient, NullLogger<HighlightClient>.Instance, usfmService);
    }
}
