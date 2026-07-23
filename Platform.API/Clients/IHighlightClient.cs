namespace Platform.API.Clients;

/// <summary>
/// Provides create, read, and delete operations for Bible verse highlights.
/// Extends both <see cref="IHighlightReader"/> (read operations, app-key auth) and
/// <see cref="IHighlightWriter"/> (write operations, OAuth bearer-token auth).
/// </summary>
/// <remarks>
/// Consumers that need only one concern should depend on
/// <see cref="IHighlightReader"/> or <see cref="IHighlightWriter"/> directly.
/// Highlight write operations require user authentication (OAuth).
/// Call <see cref="Platform.API.Extensions.ServiceCollectionExtensions.AddBibleOAuth"/> after
/// <c>AddBibleApiClients</c> to enable automatic bearer-token injection.
/// </remarks>
public interface IHighlightClient : IHighlightReader, IHighlightWriter
{
}
