using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Platform.API.Configuration;
using Platform.API.Http;
using Platform.API.Tests.Fakes;
using Xunit;

namespace Platform.API.Tests.Http;

public sealed class OutboundRateLimitingHandlerTests
{
    [Fact]
    public async Task SendAsync_AllowsRequest_WhenPermitAvailable()
    {
        var (inner, httpClient) = BuildPipeline(new BibleApiOptions
        {
            AppKey = "key",
            OutboundRequestsPerSecond = 10,
            OutboundBurstSize = 20,
            OutboundQueueLimit = 10
        });

        var response = await httpClient.GetAsync("/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_ThrowsHttpRequestException_WhenRateLimitIsExceededAndNoQueue()
    {
        var (_, httpClient) = BuildPipeline(new BibleApiOptions
        {
            AppKey = "key",
            OutboundRequestsPerSecond = 1,
            OutboundBurstSize = 1,
            OutboundQueueLimit = 0
        });

        await httpClient.GetAsync("/test");
        var act = () => httpClient.GetAsync("/test");

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*rate-limited*");
    }

    [Fact]
    public void Ctor_ThrowsInvalidOperationException_WhenBurstIsLessThanRequestsPerSecond()
    {
        var options = Options.Create(new BibleApiOptions
        {
            AppKey = "key",
            OutboundRequestsPerSecond = 10,
            OutboundBurstSize = 5,
            OutboundQueueLimit = 0
        });

        var act = () => new OutboundRateLimitingHandler(options, NullLogger<OutboundRateLimitingHandler>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OutboundBurstSize*");
    }

    private static (CapturingHandler inner, HttpClient httpClient) BuildPipeline(BibleApiOptions options)
    {
        var inner = new CapturingHandler();
        var limiter = new OutboundRateLimitingHandler(
            Options.Create(options),
            NullLogger<OutboundRateLimitingHandler>.Instance)
        {
            InnerHandler = inner
        };

        var httpClient = new HttpClient(limiter)
        {
            BaseAddress = new Uri("https://api.youversion.com")
        };

        return (inner, httpClient);
    }
}

