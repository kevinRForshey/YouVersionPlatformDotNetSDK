using FluentAssertions;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Platform.API.OAuth;

using PlatformTestApp.Auth;
using PlatformTestApp.Tests.Fakes;

using Xunit;

namespace PlatformTestApp.Tests.Auth;

public class SessionTokenProviderTests
{
    [Fact]
    public async Task StoreTokenAsync_ThenGetTokenAsync_RoundTripsToken_IncludingReceivedAt()
    {
        var cache = CreateCache();
        var provider = BuildProvider("session-abc", cache);
        var token = MakeToken("acc-tok", receivedAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        await provider.StoreTokenAsync(token);
        var retrieved = await provider.GetTokenAsync();

        retrieved.Should().NotBeNull();
        retrieved!.AccessToken.Should().Be(token.AccessToken);
        retrieved.RefreshToken.Should().Be(token.RefreshToken);
        retrieved.IdToken.Should().Be(token.IdToken);
        retrieved.TokenType.Should().Be(token.TokenType);
        retrieved.ExpiresIn.Should().Be(token.ExpiresIn);
        retrieved.ReceivedAt.Should().Be(token.ReceivedAt);
    }

    [Fact]
    public async Task StoreTokenAsync_ThrowsInvalidOperationException_WithExpectedMessage_WhenNoSessionKeyAvailable()
    {
        var provider = BuildProviderWithNoSessionKey(CreateCache());
        var token = MakeToken("acc-tok");

        var act = async () => await provider.StoreTokenAsync(token);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "No session key available to store the OAuth token. StoreTokenAsync must be called " +
                "from a request with a live HttpContext (e.g. the /auth/callback-complete endpoint).");
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsNull_WhenNoSessionKeyAvailable()
    {
        var provider = BuildProviderWithNoSessionKey(CreateCache());

        var result = await provider.GetTokenAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsNull_WhenNothingStoredForKey()
    {
        var provider = BuildProvider("session-abc", CreateCache());

        var result = await provider.GetTokenAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearTokenAsync_IsNoOp_WhenNoSessionKeyAvailable()
    {
        var provider = BuildProviderWithNoSessionKey(CreateCache());

        var act = async () => await provider.ClearTokenAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ClearTokenAsync_RemovesStoredToken()
    {
        var cache = CreateCache();
        var provider = BuildProvider("session-abc", cache);
        await provider.StoreTokenAsync(MakeToken("acc-tok"));

        await provider.ClearTokenAsync();
        var result = await provider.GetTokenAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task TwoUsers_StoreDifferentTokens_DoNotLeakBetweenSessions()
    {
        var cache = CreateCache();
        var providerA = BuildProvider("user-a-session", cache);
        var providerB = BuildProvider("user-b-session", cache);

        await providerA.StoreTokenAsync(MakeToken("user-a-token"));
        await providerB.StoreTokenAsync(MakeToken("user-b-token"));

        var tokenA = await providerA.GetTokenAsync();
        var tokenB = await providerB.GetTokenAsync();

        tokenA!.AccessToken.Should().Be("user-a-token");
        tokenB!.AccessToken.Should().Be("user-b-token");
    }

    [Fact]
    public async Task ClearTokenAsync_ForOneUser_DoesNotAffectOtherUsersToken()
    {
        var cache = CreateCache();
        var providerA = BuildProvider("user-a-session", cache);
        var providerB = BuildProvider("user-b-session", cache);
        await providerA.StoreTokenAsync(MakeToken("user-a-token"));
        await providerB.StoreTokenAsync(MakeToken("user-b-token"));

        await providerA.ClearTokenAsync();

        (await providerA.GetTokenAsync()).Should().BeNull();
        (await providerB.GetTokenAsync())!.AccessToken.Should().Be("user-b-token");
    }

    private static OAuthTokenResponse MakeToken(string accessToken, DateTimeOffset? receivedAt = null) => new()
    {
        AccessToken = accessToken,
        RefreshToken = "ref-tok",
        IdToken = "id-tok",
        TokenType = "Bearer",
        ExpiresIn = 3600,
        ReceivedAt = receivedAt ?? DateTimeOffset.UtcNow,
    };

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static SessionTokenProvider BuildProvider(string sessionId, IDistributedCache cache)
    {
        var session = new FakeSession(sessionId);
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new FakeSessionFeature(session));
        var httpContextAccessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        var keyAccessor = new CircuitSessionKeyAccessor(httpContextAccessor, CreateEmptyState());
        return new SessionTokenProvider(keyAccessor, cache);
    }

    private static SessionTokenProvider BuildProviderWithNoSessionKey(IDistributedCache cache)
    {
        var keyAccessor = new CircuitSessionKeyAccessor(new FakeHttpContextAccessor(), CreateEmptyState());
        return new SessionTokenProvider(keyAccessor, cache);
    }

    private static PersistentComponentState CreateEmptyState()
    {
        var manager = new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance);
        return manager.State;
    }
}
