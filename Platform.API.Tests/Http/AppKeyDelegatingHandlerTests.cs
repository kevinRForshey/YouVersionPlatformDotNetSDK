using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Platform.API.Configuration;
using Platform.API.Http;
using Platform.API.Tests.Fakes;
using Xunit;

namespace Platform.API.Tests.Http;

public sealed class AppKeyDelegatingHandlerTests
{
    [Fact]
    public async Task SendAsync_AddsAppKeyHeader_ToEveryRequest()
    {
        // Arrange
        var (handler, httpClient) = BuildPipeline("my-app-key");

        // Act
        await httpClient.GetAsync("/test");

        // Assert
        handler.LastRequest!.Headers.TryGetValues("X-YVP-App-Key", out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("my-app-key");
    }

    [Fact]
    public async Task SendAsync_AddsAcceptJsonHeader_ToEveryRequest()
    {
        var (handler, httpClient) = BuildPipeline("key");

        await httpClient.GetAsync("/test");

        handler.LastRequest!.Headers.TryGetValues("Accept", out var values).Should().BeTrue();
        values.Should().Contain("application/json");
    }

    [Fact]
    public async Task SendAsync_DoesNotOverwriteExistingAcceptHeader()
    {
        var (_, httpClient) = BuildPipeline("key");
        var request = new HttpRequestMessage(HttpMethod.Get, "/test");
        request.Headers.Add("Accept", "text/plain");

        // Should not throw — TryAddWithoutValidation skips duplicate
        var act = () => httpClient.SendAsync(request);
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("key-one")]
    [InlineData("another-key")]
    [InlineData("key_with_underscores")]
    public async Task SendAsync_UsesConfiguredAppKey(string appKey)
    {
        var (handler, httpClient) = BuildPipeline(appKey);

        await httpClient.GetAsync("/test");

        handler.LastRequest!.Headers.TryGetValues("X-YVP-App-Key", out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be(appKey);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (CapturingHandler handler, HttpClient httpClient) BuildPipeline(string appKey)
    {
        var options = Options.Create(new YouVersionApiOptions { AppKey = appKey });
        var inner = new CapturingHandler();
        var sut = new AppKeyDelegatingHandler(options) { InnerHandler = inner };
        var httpClient = new HttpClient(sut)
        {
            BaseAddress = new Uri("https://api.youversion.com")
        };
        return (inner, httpClient);
    }
}
