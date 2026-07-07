using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Platform.API.Clients;
using Platform.API.Configuration;
using Platform.API.Http;
using Platform.API.OAuth;

using YouVersion.UsfmReferences;

namespace Platform.API.Extensions;

/// <summary>
/// Extension methods for registering YouVersion Platform API clients with an
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the YouVersion Platform API clients (<see cref="IBibleClient"/>,
    /// <see cref="IPassageClient"/>, and <see cref="IHighlightClient"/>) and their
    /// supporting infrastructure with the dependency-injection container.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureOptions">
    /// A delegate to configure <see cref="YouVersionApiOptions"/>.
    /// At minimum, set <see cref="YouVersionApiOptions.AppKey"/>.
    /// </param>
    /// <returns>The original <paramref name="services"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddYouVersionApiClients(options =>
    /// {
    ///     options.AppKey = builder.Configuration["YouVersionApi:AppKey"]!;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddYouVersionApiClients(
        this IServiceCollection services,
        Action<YouVersionApiOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddOptions<YouVersionApiOptions>()
            .Configure(configureOptions)
            .ValidateOnStart();

        services.AddTransient<AppKeyDelegatingHandler>();
        services.AddTransient<OutboundRateLimitingHandler>();

        // Register USFM reference service as singleton (stateless, thread-safe)
        services.TryAddSingleton<IUsfmReferenceService, UsfmReferenceService>();

        RegisterTypedClient<IBibleClient, BibleClient>(services);
        RegisterTypedClient<IPassageClient, PassageClient>(services);
        RegisterTypedClient<IHighlightClient, HighlightClient>(services);
        return services;
    }

    /// <summary>
    /// Registers the YouVersion Platform API clients by binding
    /// <see cref="YouVersionApiOptions"/> from the <c>YouVersionApi</c> configuration section.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">
    /// An <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> instance.
    /// The <c>YouVersionApi</c> section must contain at least an <c>AppKey</c> value.
    /// </param>
    /// <returns>The original <paramref name="services"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// // appsettings.json:  { "YouVersionApi": { "AppKey": "your-key-here" } }
    /// builder.Services.AddYouVersionApiClients(builder.Configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddYouVersionApiClients(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<YouVersionApiOptions>()
            .Bind(configuration.GetSection(YouVersionApiOptions.SectionName))
            .ValidateOnStart();

        services.AddTransient<AppKeyDelegatingHandler>();
        services.AddTransient<OutboundRateLimitingHandler>();

        // Register USFM reference service as singleton (stateless, thread-safe)
        services.TryAddSingleton<IUsfmReferenceService, UsfmReferenceService>();

        RegisterTypedClient<IBibleClient, BibleClient>(services);
        RegisterTypedClient<IPassageClient, PassageClient>(services);
        RegisterTypedClient<IHighlightClient, HighlightClient>(services);

        return services;
    }

    /// <summary>
    /// Registers the YouVersion OAuth 2.0 service (<see cref="IYouVersionOAuthClient"/>),
    /// an <see cref="InMemoryTokenProvider"/> as the default <see cref="ITokenProvider"/>,
    /// and upgrades <see cref="IHighlightClient"/> to use bearer-token authentication.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="YouVersionOAuthOptions"/>.</param>
    /// <returns>The original <paramref name="services"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddYouVersionApiClients(o => o.AppKey = "my-key")
    ///                 .AddYouVersionOAuth(o =>
    ///                 {
    ///                     o.Scopes = "highlights";
    ///                 });
    /// </code>
    /// </example>
    public static IServiceCollection AddYouVersionOAuth(
        this IServiceCollection services,
        Action<YouVersionOAuthOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        if (!HasApiClientRegistration(services))
            throw new InvalidOperationException(
                "AddYouVersionApiClients must be called before AddYouVersionOAuth so API options and HTTP pipelines are configured.");

        services.AddOptions<YouVersionOAuthOptions>()
            .Configure(configureOptions)
            .ValidateOnStart();

        // Default in-memory token provider
        // by registering ITokenProvider BEFORE calling AddYouVersionOAuth.
        services.TryAddSingleton<ITokenProvider, InMemoryTokenProvider>();

        services.AddTransient<OAuthBearerTokenHandler>();

        // OAuth HTTP client — no app key or bearer required on auth endpoints.
        // BaseAddress is intentionally not set; PostTokenRequestAsync uses the absolute
        // TokenEndpoint URI from YouVersionOAuthOptions directly.
        services
            .AddHttpClient<IYouVersionOAuthClient, YouVersionOAuthClient>()
            .AddHttpMessageHandler<OutboundRateLimitingHandler>()
            .AddStandardResilienceHandler();

        // Append OAuthBearerTokenHandler to IHighlightClient's existing pipeline
        // (AppKeyDelegatingHandler was already added by AddYouVersionApiClients).
        // AddYouVersionApiClients MUST be called first.
        services.AddHttpClient(typeof(HighlightClient).Name)
            .AddHttpMessageHandler<OAuthBearerTokenHandler>();

        return services;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static void RegisterTypedClient<TClient, TImplementation>(IServiceCollection services)
        where TClient : class
        where TImplementation : class, TClient
    {
        // Register the concrete implementation as its own typed HTTP client so it can be
        // resolved directly by name (e.g. by caching decorators without creating a circular
        // dependency through the interface).
        services
            .AddHttpClient<TImplementation>((serviceProvider, httpClient) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<YouVersionApiOptions>>()
                    .Value;

                httpClient.BaseAddress = options.BaseAddress;
                httpClient.Timeout = options.Timeout;
            })
            .AddHttpMessageHandler<AppKeyDelegatingHandler>()
            .AddHttpMessageHandler<OutboundRateLimitingHandler>()
            .AddStandardResilienceHandler();

        // Forward the public interface to the concrete implementation.
        // AddYouVersionCaching() replaces this registration with a caching decorator.
        services.AddTransient<TClient>(sp => sp.GetRequiredService<TImplementation>());
    }

    /// <summary>
    /// Wraps <see cref="IBibleClient"/> and <see cref="IPassageClient"/> with caching decorators
    /// backed by <see cref="HybridCache"/> (L1 in-process memory + optional L2 distributed cache).
    /// Must be called after <see cref="AddYouVersionApiClients(IServiceCollection, Action{YouVersionApiOptions})"/>.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configureOptions">
    /// Optional delegate to customize per-data-type TTLs. When omitted, defaults are
    /// 24 h for version/book data and 7 days for passage content.
    /// </param>
    /// <remarks>
    /// To add a distributed (L2) cache, register an <c>IDistributedCache</c> implementation
    /// (e.g. <c>AddStackExchangeRedisCache</c>) before or after calling this method.
    /// <see cref="HybridCache"/> will detect it automatically and use it as the L2 tier.
    /// </remarks>
    /// <returns>The original <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddYouVersionCaching(
        this IServiceCollection services,
        Action<YouVersionCacheOptions>? configureOptions = null)
    {
        if (!HasApiClientRegistration(services))
            throw new InvalidOperationException(
                "AddYouVersionApiClients must be called before AddYouVersionCaching.");

        var opts = new YouVersionCacheOptions();
        configureOptions?.Invoke(opts);
        services.AddSingleton(opts);

        // ── Redis L2 ────────────────────────────────────────────────────────
        // Register Redis before HybridCache so HybridCache discovers it as L2.
        // Skipped when no connection string is provided — L1-only mode.
        if (!string.IsNullOrWhiteSpace(opts.RedisConnectionString))
        {
            services.AddStackExchangeRedisCache(o =>
                o.Configuration = opts.RedisConnectionString);
        }

        // ── In-process L1 ───────────────────────────────────────────────────
        // SizeLimit enables LRU eviction so the process cache doesn't grow without bound.
        if (opts.MaxL1Entries.HasValue)
            services.AddMemoryCache(o => o.SizeLimit = opts.MaxL1Entries.Value);
        else
            services.AddMemoryCache();

        // ── HybridCache ─────────────────────────────────────────────────────
        // Automatically uses the registered IDistributedCache (Redis) as L2 when present.
        // MaximumPayloadBytes prevents a single oversized response from filling L1 —
        // the value is still returned to the caller but not written to either tier.
        services.AddHybridCache(o => o.MaximumPayloadBytes = 4 * 1024 * 1024); // 4 MB

        // Replace the plain interface → implementation forward with caching decorators.
        // The underlying BibleClient/PassageClient typed HTTP clients remain registered and
        // are resolved directly by the decorators to avoid circular dependency.
        services.Replace(ServiceDescriptor.Transient<IBibleClient>(sp =>
            new CachingBibleClient(
                sp.GetRequiredService<BibleClient>(),
                sp.GetRequiredService<HybridCache>(),
                sp.GetRequiredService<YouVersionCacheOptions>())));

        services.Replace(ServiceDescriptor.Transient<IPassageClient>(sp =>
            new CachingPassageClient(
                sp.GetRequiredService<PassageClient>(),
                sp.GetRequiredService<HybridCache>(),
                sp.GetRequiredService<YouVersionCacheOptions>())));

        return services;
    }

    private static bool HasApiClientRegistration(IServiceCollection services)
        => services.Any(static d => d.ServiceType == typeof(IConfigureOptions<YouVersionApiOptions>));
}
