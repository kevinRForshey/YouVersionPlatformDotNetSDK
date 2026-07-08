using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace PlatformTestApp.Auth;

/// <summary>
/// Resolves a stable per-browser key identifying which OAuth token belongs to the current user,
/// usable both during the initial HTTP request (prerender, where <see cref="HttpContext"/> is
/// live) and for the remainder of the interactive Blazor Server circuit (where it is not).
/// </summary>
/// <remarks>
/// During prerender the key is the ASP.NET Core session id, captured via
/// <see cref="PersistentComponentState"/> so it survives the handoff to the interactive circuit —
/// Blazor's supported mechanism for carrying prerender-only data (here, the session id, since
/// <see cref="IHttpContextAccessor.HttpContext"/> is always <see langword="null"/> once a
/// component is running on its live SignalR circuit) forward without needing HttpContext again.
/// </remarks>
public sealed class CircuitSessionKeyAccessor : IDisposable
{
    private const string PersistKey = "yv_session_key";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription _subscription;
    private string? _key;

    public CircuitSessionKeyAccessor(IHttpContextAccessor httpContextAccessor, PersistentComponentState state)
    {
        _httpContextAccessor = httpContextAccessor;
        _state = state;
        _subscription = state.RegisterOnPersisting(PersistKeyAsync);
    }

    /// <summary>
    /// Returns the stable per-browser key, or <see langword="null"/> if none is available yet
    /// (no live <see cref="HttpContext"/> and nothing was persisted from a prior prerender).
    /// </summary>
    public string? GetKey()
    {
        if (_key is not null)
            return _key;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            _key = httpContext.Session.Id;
            return _key;
        }

        if (_state.TryTakeFromJson<string>(PersistKey, out var restored))
            _key = restored;

        return _key;
    }

    private Task PersistKeyAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
            _state.PersistAsJson(PersistKey, httpContext.Session.Id);

        return Task.CompletedTask;
    }

    public void Dispose() => _subscription.Dispose();
}
