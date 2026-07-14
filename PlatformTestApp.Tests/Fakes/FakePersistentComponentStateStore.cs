using Microsoft.AspNetCore.Components;

namespace PlatformTestApp.Tests.Fakes;

internal sealed class FakePersistentComponentStateStore : IPersistentComponentStateStore
{
    private readonly IDictionary<string, byte[]> _state;

    public FakePersistentComponentStateStore(IDictionary<string, byte[]> state) => _state = state;

    public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() => Task.FromResult(_state);

    public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state) => Task.CompletedTask;

    public bool SupportsRenderMode(IComponentRenderMode renderMode) => true;
}
