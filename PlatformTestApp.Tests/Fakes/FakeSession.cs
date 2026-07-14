using Microsoft.AspNetCore.Http;

namespace PlatformTestApp.Tests.Fakes;

internal sealed class FakeSession : ISession
{
    public FakeSession(string id) => Id = id;

    public string Id { get; set; }

    public bool IsAvailable => true;

    public IEnumerable<string> Keys => [];

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public bool TryGetValue(string key, out byte[] value)
    {
        value = [];
        return false;
    }

    public void Set(string key, byte[] value)
    {
    }

    public void Remove(string key)
    {
    }

    public void Clear()
    {
    }
}
