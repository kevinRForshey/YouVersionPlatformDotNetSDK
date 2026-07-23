using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.API.Clients;
using Platform.API.Exceptions;
using Platform.API.Tests.Fakes;
using Xunit;

namespace Platform.API.Tests.Clients;

public sealed class HighlightClientTests
{
    private const string HighlightJson = """{ "bible_id": 3034, "passage_id": "JHN.3.16", "color": "44aa44" }""";

    private const string HighlightsCollectionJson = """
        { "data": [ { "bible_id": 3034, "passage_id": "JHN.3.16", "color": "44aa44" } ] }
        """;

    private const string RecentColorsJson = """{ "data": [ { "color": "44aa44" }, { "color": "ffd54f" } ] }""";

    // -------------------------------------------------------------------------
    // GetHighlightsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetHighlightsAsync_ReturnsHighlights_WhenApiSucceeds()
    {
        var client = BuildClient(HttpStatusCode.OK, HighlightsCollectionJson);

        var result = await client.GetHighlightsAsync(3034, TestReferences.John316);

        result.Should().HaveCount(1);
        result[0].BibleId.Should().Be(3034);
        result[0].PassageId.Should().Be("JHN.3.16");
        result[0].Color.Should().Be("44aa44");
    }

    [Fact]
    public async Task GetHighlightsAsync_SendsBibleIdAndPassageIdAsQueryParams()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, HighlightsCollectionJson);
        var client = BuildClientFromHandler(handler);

        await client.GetHighlightsAsync(3034, TestReferences.Genesis1);

        var query = handler.LastRequest!.RequestUri!.Query;
        query.Should().Contain("bible_id=3034");
        query.Should().Contain("passage_id=GEN.1");
    }

    [Fact]
    public async Task GetHighlightsAsync_ThrowsArgumentOutOfRangeException_WhenBibleIdIsNotPositive()
    {
        var client = BuildClient(HttpStatusCode.OK, HighlightsCollectionJson);
        var act = () => client.GetHighlightsAsync(0, TestReferences.John316);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetHighlightsAsync_ThrowsBibleApiException_OnError(HttpStatusCode status)
    {
        var client = BuildClient(status, """{"error":"fail"}""");
        var act = () => client.GetHighlightsAsync(3034, TestReferences.John316);
        await act.Should().ThrowAsync<BibleApiException>()
            .Where(e => e.StatusCode == status);
    }

    // -------------------------------------------------------------------------
    // GetRecentColorsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetRecentColorsAsync_ReturnsColors_WhenApiSucceeds()
    {
        var client = BuildClient(HttpStatusCode.OK, RecentColorsJson);

        var result = await client.GetRecentColorsAsync();

        result.Should().Equal("44aa44", "ffd54f");
    }

    [Fact]
    public async Task GetRecentColorsAsync_SendsGetRequestToRecentColorsPath()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, RecentColorsJson);
        var client = BuildClientFromHandler(handler);

        await client.GetRecentColorsAsync();

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/v1/highlights/recent-colors");
    }

    // -------------------------------------------------------------------------
    // CreateOrUpdateHighlightAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrUpdateHighlightAsync_ReturnsSavedHighlight_WhenApiSucceeds()
    {
        var client = BuildClient(HttpStatusCode.OK, HighlightJson);

        var highlight = await client.CreateOrUpdateHighlightAsync(3034, TestReferences.John316, "44aa44");

        highlight.BibleId.Should().Be(3034);
        highlight.PassageId.Should().Be("JHN.3.16");
        highlight.Color.Should().Be("44aa44");
    }

    [Fact]
    public async Task CreateOrUpdateHighlightAsync_SendsPostRequestWithRequestIdAndHighlightEnvelope()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, HighlightJson);
        var client = BuildClientFromHandler(handler);

        await client.CreateOrUpdateHighlightAsync(3034, TestReferences.John316, "44aa44");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        using var json = JsonDocument.Parse(handler.LastRequestBody!);
        json.RootElement.GetProperty("request_id").GetGuid().Should().NotBeEmpty();
        var highlight = json.RootElement.GetProperty("highlight");
        highlight.GetProperty("bible_id").GetInt32().Should().Be(3034);
        highlight.GetProperty("passage_id").GetString().Should().Be("JHN.3.16");
        highlight.GetProperty("color").GetString().Should().Be("44aa44");
    }

    [Fact]
    public async Task CreateOrUpdateHighlightAsync_ThrowsBibleApiException_OnError()
    {
        var client = BuildClient(HttpStatusCode.Unauthorized, """{"error":"unauthorized"}""");
        var act = () => client.CreateOrUpdateHighlightAsync(3034, TestReferences.John316, "44aa44");
        await act.Should().ThrowAsync<BibleApiException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateOrUpdateHighlightAsync_ThrowsArgumentOutOfRangeException_WhenBibleIdIsNotPositive()
    {
        var client = BuildClient(HttpStatusCode.OK, HighlightJson);
        var act = () => client.CreateOrUpdateHighlightAsync(0, TestReferences.John316, "44aa44");
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-color")]
    [InlineData("#44aa44")]
    [InlineData("44aa4")]
    public async Task CreateOrUpdateHighlightAsync_ThrowsArgumentException_WhenColorIsNotHex(string color)
    {
        var client = BuildClient(HttpStatusCode.OK, HighlightJson);
        var act = () => client.CreateOrUpdateHighlightAsync(3034, TestReferences.John316, color);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // ClearHighlightsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClearHighlightsAsync_Succeeds_WhenApiReturnsNoContent()
    {
        var client = BuildClient(HttpStatusCode.NoContent, string.Empty);
        var act = () => client.ClearHighlightsAsync(3034, TestReferences.John316);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ClearHighlightsAsync_SendsDeleteRequestWithPassageIdInPathAndBibleIdAsQuery()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var client = BuildClientFromHandler(handler);

        await client.ClearHighlightsAsync(3034, TestReferences.John316);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().EndWith("JHN.3.16");
        handler.LastRequest.RequestUri.Query.Should().Contain("bible_id=3034");
    }

    [Fact]
    public async Task ClearHighlightsAsync_ThrowsBibleApiException_WhenNotFound()
    {
        var client = BuildClient(HttpStatusCode.NotFound, """{"error":"not found"}""");
        var act = () => client.ClearHighlightsAsync(3034, TestReferences.John316);
        await act.Should().ThrowAsync<BibleApiException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ClearHighlightsAsync_ThrowsArgumentOutOfRangeException_WhenBibleIdIsNotPositive()
    {
        var client = BuildClient(HttpStatusCode.NoContent, string.Empty);
        var act = () => client.ClearHighlightsAsync(0, TestReferences.John316);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static HighlightClient BuildClient(HttpStatusCode status, string json)
        => BuildClientFromHandler(new FakeHttpMessageHandler(status, json));

    private static HighlightClient BuildClientFromHandler(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.youversion.com") };
        return new HighlightClient(httpClient, NullLogger<HighlightClient>.Instance);
    }
}
