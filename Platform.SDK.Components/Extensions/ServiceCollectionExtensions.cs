using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.SDK.Services;

namespace Platform.SDK.Components.Extensions;

/// <summary>
/// Extension methods for registering the SDK's Blazor components and their services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SDK services required by the Blazor components:
    /// <see cref="IVersionService"/>, <see cref="IBookService"/>, <see cref="IChapterService"/>,
    /// <see cref="IPassageService"/>, <see cref="IHighlightService"/>,
    /// <see cref="IBibleReaderStateService"/>, and <see cref="IAuthSessionService"/>.
    /// Call this after <c>AddBibleApiClients</c> and <c>AddBibleOAuth</c>.
    /// </summary>
    public static IServiceCollection AddBibleComponents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IVersionService, VersionService>();
        services.TryAddScoped<IBookService, BookService>();
        services.TryAddScoped<IChapterService, ChapterService>();
        services.TryAddScoped<IPassageService, PassageService>();
        services.TryAddScoped<IHighlightService, HighlightService>();
        services.TryAddScoped<IBibleReaderStateService, BibleReaderStateService>();
        services.TryAddScoped<IAuthSessionService, AuthSessionService>();

        return services;
    }
}
