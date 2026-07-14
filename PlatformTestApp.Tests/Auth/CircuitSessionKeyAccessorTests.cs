using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

using PlatformTestApp.Auth;
using PlatformTestApp.Tests.Fakes;

using Xunit;

namespace PlatformTestApp.Tests.Auth;

public class CircuitSessionKeyAccessorTests
{
    private const string PersistKey = "yv_session_key";

    [Fact]
    public void GetKey_ReturnsHttpContextSessionId_WhenHttpContextIsAvailable()
    {
        var accessor = BuildAccessorWithLiveSession("session-abc", out _);

        accessor.GetKey().Should().Be("session-abc");
    }

    [Fact]
    public void GetKey_CachesFirstResolvedKey_AndIgnoresLaterSessionIdChanges()
    {
        var accessor = BuildAccessorWithLiveSession("first-id", out var session);

        var first = accessor.GetKey();
        session.Id = "second-id";
        var second = accessor.GetKey();

        first.Should().Be("first-id");
        second.Should().Be("first-id");
    }

    [Fact]
    public void GetKey_ReturnsNull_WhenHttpContextIsNullAndNothingWasPersisted()
    {
        var accessor = new CircuitSessionKeyAccessor(new FakeHttpContextAccessor(), CreateEmptyState());

        accessor.GetKey().Should().BeNull();
    }

    [Fact]
    public async Task GetKey_ReturnsRestoredKey_WhenHttpContextIsNullButStateWasPersisted()
    {
        var state = await CreateRestoredStateAsync(PersistKey, "restored-key");
        var accessor = new CircuitSessionKeyAccessor(new FakeHttpContextAccessor(), state);

        accessor.GetKey().Should().Be("restored-key");
    }

    [Fact]
    public async Task GetKey_PrefersLiveHttpContextSession_OverPersistedState()
    {
        var state = await CreateRestoredStateAsync(PersistKey, "persisted-key");
        var session = new FakeSession("live-key");
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new FakeSessionFeature(session));
        var httpContextAccessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        var accessor = new CircuitSessionKeyAccessor(httpContextAccessor, state);

        accessor.GetKey().Should().Be("live-key");
    }

    [Fact]
    public void TwoAccessors_WithDifferentHttpContextSessions_ResolveDifferentKeys()
    {
        var accessorA = BuildAccessorWithLiveSession("user-a-session", out _);
        var accessorB = BuildAccessorWithLiveSession("user-b-session", out _);

        accessorA.GetKey().Should().Be("user-a-session");
        accessorB.GetKey().Should().Be("user-b-session");
    }

    [Fact]
    public void Dispose_DisposesPersistingSubscription_WithoutThrowing()
    {
        var accessor = BuildAccessorWithLiveSession("session-abc", out _);

        var act = accessor.Dispose;

        act.Should().NotThrow();
    }

    private static CircuitSessionKeyAccessor BuildAccessorWithLiveSession(string sessionId, out FakeSession session)
    {
        session = new FakeSession(sessionId);
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new FakeSessionFeature(session));
        var httpContextAccessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        return new CircuitSessionKeyAccessor(httpContextAccessor, CreateEmptyState());
    }

    private static PersistentComponentState CreateEmptyState()
    {
        var manager = new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance);
        return manager.State;
    }

    private static async Task<PersistentComponentState> CreateRestoredStateAsync(string key, string value)
    {
        var manager = new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance);
        var stored = new Dictionary<string, byte[]> { [key] = JsonSerializer.SerializeToUtf8Bytes(value) };
        await manager.RestoreStateAsync(new FakePersistentComponentStateStore(stored));
        return manager.State;
    }
}
