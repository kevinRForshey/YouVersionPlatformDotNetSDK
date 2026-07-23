using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Platform.API.Http;
using Platform.API.OAuth;
using Platform.API.Tests.Fakes;
using Xunit;

namespace Platform.API.Tests.Http;

public sealed class OAuthBearerTokenHandlerTests
{
    // -------------------------------------------------------------------------
    // Token present and valid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_AddsBearerToken_WhenTokenIsValid()
    {
        var token = FreshToken("valid-access-token");
        var (inner, httpClient) = BuildPipeline(token);

        await httpClient.GetAsync("/test");

        inner.LastRequest!.Headers.Authorization.Should().NotBeNull();
        inner.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        inner.LastRequest.Headers.Authorization!.Parameter.Should().Be("valid-access-token");
    }

    // -------------------------------------------------------------------------
    // No token stored
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_DoesNotAddAuthHeader_WhenNoTokenStored()
    {
        var (inner, httpClient) = BuildPipeline(storedToken: null);

        await httpClient.GetAsync("/test");

        inner.LastRequest!.Headers.Authorization.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Expired token — refresh succeeds
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_RefreshesToken_WhenStoredTokenIsExpired()
    {
        var expiredToken = new OAuthTokenResponse
        {
            AccessToken = "old-access", RefreshToken = "ref-tok",
            ExpiresIn = 10, ReceivedAt = DateTimeOffset.UtcNow.AddSeconds(-100)
        };
        var refreshedToken = FreshToken("refreshed-access");

        var oAuthMock = new Mock<IBibleOAuthClient>();
        oAuthMock.Setup(c => c.RefreshTokenAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(refreshedToken);

        var (inner, httpClient) = BuildPipeline(expiredToken, oAuthMock.Object);

        await httpClient.GetAsync("/test");

        oAuthMock.Verify(c => c.RefreshTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
        inner.LastRequest!.Headers.Authorization!.Parameter.Should().Be("refreshed-access");
    }

    // -------------------------------------------------------------------------
    // Expired token — refresh fails (no refresh token)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_ProceedsWithoutAuth_WhenRefreshFails()
    {
        var expiredToken = new OAuthTokenResponse
        {
            AccessToken = "old-access", RefreshToken = null,
            ExpiresIn = 10, ReceivedAt = DateTimeOffset.UtcNow.AddSeconds(-100)
        };

        var oAuthMock = new Mock<IBibleOAuthClient>();
        oAuthMock.Setup(c => c.RefreshTokenAsync(It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("No refresh token available."));

        var (inner, httpClient) = BuildPipeline(expiredToken, oAuthMock.Object);

        // Should not throw — handler swallows the InvalidOperationException
        await httpClient.GetAsync("/test");

        inner.LastRequest!.Headers.Authorization.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Fresh token — refresh is NOT called
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_DoesNotRefresh_WhenTokenIsFresh()
    {
        var freshToken = FreshToken("fresh-access");
        var oAuthMock = new Mock<IBibleOAuthClient>();

        var (_, httpClient) = BuildPipeline(freshToken, oAuthMock.Object);

        await httpClient.GetAsync("/test");

        oAuthMock.Verify(c => c.RefreshTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Expired token — concurrent requests single-flight the refresh
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_SingleFlightsRefresh_WhenConcurrentRequestsHitExpiredToken()
    {
        var expiredToken = new OAuthTokenResponse
        {
            AccessToken = "old-access", RefreshToken = "ref-tok",
            ExpiresIn = 10, ReceivedAt = DateTimeOffset.UtcNow.AddSeconds(-100)
        };
        var refreshedToken = FreshToken("refreshed-access");

        var refreshTcs = new TaskCompletionSource<OAuthTokenResponse>();
        var refreshCallCount = 0;
        var oAuthMock = new Mock<IBibleOAuthClient>();
        oAuthMock.Setup(c => c.RefreshTokenAsync(It.IsAny<CancellationToken>()))
                 .Returns(() =>
                 {
                     Interlocked.Increment(ref refreshCallCount);
                     return refreshTcs.Task;
                 });

        var (_, httpClient) = BuildPipeline(expiredToken, oAuthMock.Object);

        // Several requests race in while the token is expired and no refresh has completed yet.
        var requestTasks = Enumerable.Range(0, 5)
            .Select(_ => httpClient.GetAsync("/test"))
            .ToArray();

        refreshTcs.SetResult(refreshedToken);
        await Task.WhenAll(requestTasks);

        refreshCallCount.Should().Be(1);
        oAuthMock.Verify(c => c.RefreshTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static OAuthTokenResponse FreshToken(string accessToken) =>
        new() { AccessToken = accessToken, ExpiresIn = 3600, ReceivedAt = DateTimeOffset.UtcNow };

    private static (CapturingHandler inner, HttpClient httpClient) BuildPipeline(
        OAuthTokenResponse? storedToken,
        IBibleOAuthClient? oAuthClient = null)
    {
        var tokenProvider = new FakeTokenProvider(storedToken);
        var oAuth = oAuthClient ?? Mock.Of<IBibleOAuthClient>();
        var apiOptions = Options.Create(new BibleOAuthOptions
        {
            ClientId = "test-client",
            OAuthTokenExpiryBufferSeconds = 60
        });

        var inner = new CapturingHandler();
        var sut = new OAuthBearerTokenHandler(tokenProvider, oAuth, apiOptions,
            NullLogger<OAuthBearerTokenHandler>.Instance)
        {
            InnerHandler = inner
        };

        var httpClient = new HttpClient(sut) { BaseAddress = new Uri("https://api.youversion.com") };
        return (inner, httpClient);
    }
}
