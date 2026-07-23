using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Platform.API.Clients;
using Platform.API.Extensions;
using Platform.API.OAuth;
using Platform.API.Tests.Fakes;
using BiblePlatform.UsfmReferences;
using Xunit;

namespace Platform.API.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    // -------------------------------------------------------------------------
    // AddBibleApiClients — inline options
    // -------------------------------------------------------------------------

    [Fact]
    public void AddBibleApiClients_RegistersIBibleClient()
    {
        var sp = BuildProvider(withOAuth: false);
        sp.GetRequiredService<IBibleClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddBibleApiClients_RegistersIPassageClient()
    {
        var sp = BuildProvider(withOAuth: false);
        sp.GetRequiredService<IPassageClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddBibleApiClients_RegistersIHighlightClient()
    {
        var sp = BuildProvider(withOAuth: false);
        sp.GetRequiredService<IHighlightClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddBibleApiClients_RegistersIUsfmReferenceService_AsSingleton()
    {
        var sp = BuildProvider(withOAuth: false);

        var service1 = sp.GetRequiredService<IUsfmReferenceService>();
        var service2 = sp.GetRequiredService<IUsfmReferenceService>();

        service1.Should().NotBeNull();
        service1.Should().BeOfType<UsfmReferenceService>();
        service1.Should().BeSameAs(service2);
    }

    [Fact]
    public void AddBibleApiClients_ResolvesDistinctInstances_PerScope()
    {
        var sp = BuildProvider(withOAuth: false);

        using var scope1 = sp.CreateScope();
        using var scope2 = sp.CreateScope();

        var client1 = scope1.ServiceProvider.GetRequiredService<IBibleClient>();
        var client2 = scope2.ServiceProvider.GetRequiredService<IBibleClient>();

        // Typed HttpClient registrations create a new instance per resolution
        client1.Should().NotBeSameAs(client2);
    }

    // -------------------------------------------------------------------------
    // AddBibleOAuth
    // -------------------------------------------------------------------------

    [Fact]
    public void AddBibleOAuth_RegistersIBibleOAuthClient()
    {
        var sp = BuildProvider(withOAuth: true);
        sp.GetRequiredService<IBibleOAuthClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddBibleOAuth_RegistersITokenProvider_AsInMemoryTokenProvider()
    {
        var sp = BuildProvider(withOAuth: true);
        sp.GetRequiredService<ITokenProvider>().Should().BeOfType<InMemoryTokenProvider>();
    }

    [Fact]
    public void AddBibleOAuth_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var returned = services.AddBibleApiClients(o => o.AppKey = "key")
                               .AddBibleOAuth(o => o.ClientId = "cid");

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddBibleOAuth_ThrowsInvalidOperationException_WhenApiClientsNotRegisteredFirst()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var act = () => services.AddBibleOAuth(o => o.ClientId = "cid");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AddBibleApiClients*");
    }

    [Fact]
    public void AddBibleApiClients_ThrowsArgumentNullException_WhenServicesIsNull()
    {
        IServiceCollection services = null!;

        var act = () => services.AddBibleApiClients(o => o.AppKey = "key");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddBibleApiClients_ThrowsArgumentNullException_WhenConfigureIsNull()
    {
        var services = new ServiceCollection();

        var act = () => services.AddBibleApiClients((Action<Configuration.BibleApiOptions>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------------
    // End-to-end: bearer token actually flows through IHighlightClient's pipeline
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddBibleOAuth_AttachesBearerToken_ToLiveHighlightClientRequest()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBibleApiClients(o => o.AppKey = "test-key");
        services.AddBibleOAuth(o =>
        {
            o.ClientId = "test-client";
            o.RedirectUri = new Uri("https://localhost/callback");
        });

        // Intercept the same named pipeline AddBibleOAuth appended the bearer handler to,
        // via the shared constant rather than re-deriving the client name independently.
        var capturingHandler = new CapturingHandler(
            responseBody: """{"data":[]}""");
        services.AddHttpClient(ServiceCollectionExtensions.HighlightClientName)
            .ConfigurePrimaryHttpMessageHandler(() => capturingHandler);

        var sp = services.BuildServiceProvider();

        var tokenProvider = sp.GetRequiredService<ITokenProvider>();
        await tokenProvider.StoreTokenAsync(new OAuthTokenResponse
        {
            AccessToken = "live-access-token",
            ExpiresIn = 3600,
            ReceivedAt = DateTimeOffset.UtcNow
        });

        var highlightClient = sp.GetRequiredService<IHighlightClient>();
        await highlightClient.GetHighlightsAsync(3034, Reference.FromString("JHN.3.16"));

        capturingHandler.LastRequest!.Headers.Authorization.Should().NotBeNull();
        capturingHandler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturingHandler.LastRequest.Headers.Authorization!.Parameter.Should().Be("live-access-token");
    }

    // -------------------------------------------------------------------------
    // Custom ITokenProvider replacement
    // -------------------------------------------------------------------------

    [Fact]
    public void AddBibleOAuth_AllowsCustomTokenProvider_Override()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBibleApiClients(o => o.AppKey = "key");

        // Register custom provider BEFORE AddBibleOAuth
        services.AddSingleton<ITokenProvider, CustomTokenProvider>();
        services.AddBibleOAuth(o => o.ClientId = "cid");

        // The first registered singleton wins
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<ITokenProvider>().Should().BeOfType<CustomTokenProvider>();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ServiceProvider BuildProvider(bool withOAuth)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBibleApiClients(o => o.AppKey = "test-key");

        if (withOAuth)
        {
            services.AddBibleOAuth(o =>
            {
                o.ClientId = "test-client";
                o.RedirectUri = new Uri("https://localhost/callback");
            });
        }

        return services.BuildServiceProvider();
    }

    private sealed class CustomTokenProvider : ITokenProvider
    {
        public Task<OAuthTokenResponse?> GetTokenAsync(
            CancellationToken ct = default)
            => Task.FromResult<OAuthTokenResponse?>(null);

        public Task StoreTokenAsync(OAuthTokenResponse token,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task ClearTokenAsync(
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
