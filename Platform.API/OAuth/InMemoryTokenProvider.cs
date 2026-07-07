namespace Platform.API.OAuth;

/// <summary>
/// A simple in-process, non-persistent <see cref="ITokenProvider"/> suitable for
/// console applications, background services, and tests.
/// </summary>
/// <remarks>
/// Tokens are stored in a private field and lost when the process exits.
/// thread safe implementation.
/// Use a custom <see cref="ITokenProvider"/> implementation for scenarios that require
/// durable token storage (mobile, web, or desktop applications).
/// </remarks>
public sealed class InMemoryTokenProvider : ITokenProvider, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private OAuthTokenResponse? _token;

    /// <inheritdoc />
    public async Task<OAuthTokenResponse?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task StoreTokenAsync(OAuthTokenResponse token, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _token = token;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearTokenAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _token = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _lock.Dispose();
}
