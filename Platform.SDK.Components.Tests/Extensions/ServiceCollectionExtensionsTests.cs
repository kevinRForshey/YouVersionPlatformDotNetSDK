using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Platform.API.Clients;
using Platform.SDK.Components.Extensions;
using Platform.SDK.Services;
using Xunit;

namespace Platform.SDK.Components.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddYouVersionComponents_RegistersAllSixServicesAsScoped()
    {
        var services = new ServiceCollection();

        services.AddYouVersionComponents();

        services.Should().Contain(d => d.ServiceType == typeof(IVersionService) && d.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(d => d.ServiceType == typeof(IBookService) && d.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(d => d.ServiceType == typeof(IChapterService) && d.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(d => d.ServiceType == typeof(IPassageService) && d.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(d => d.ServiceType == typeof(IHighlightService) && d.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(d => d.ServiceType == typeof(IBibleReaderStateService) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddYouVersionComponents_ReturnsSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddYouVersionComponents();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddYouVersionComponents_DoesNotOverrideExistingRegistrations()
    {
        var services = new ServiceCollection();
        services.AddScoped<IVersionService, FakeVersionService>();

        services.AddYouVersionComponents();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IVersionService>().Should().BeOfType<FakeVersionService>();
    }

    [Fact]
    public void AddYouVersionComponents_ResolvesAllRegisteredServicesFromContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IBibleClient>());
        services.AddSingleton(Mock.Of<IPassageClient>());
        services.AddSingleton(Mock.Of<IHighlightClient>());
        services.AddYouVersionComponents();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IVersionService>().Should().BeOfType<VersionService>();
        provider.GetRequiredService<IBookService>().Should().BeOfType<BookService>();
        provider.GetRequiredService<IChapterService>().Should().BeOfType<ChapterService>();
        provider.GetRequiredService<IPassageService>().Should().BeOfType<PassageService>();
        provider.GetRequiredService<IHighlightService>().Should().BeOfType<HighlightService>();
        provider.GetRequiredService<IBibleReaderStateService>().Should().BeOfType<BibleReaderStateService>();
    }

    [Fact]
    public void AddYouVersionComponents_NullServices_ThrowsArgumentNullException()
    {
        ServiceCollection? services = null;

        var act = () => services!.AddYouVersionComponents();

        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class FakeVersionService : IVersionService
    {
        public Task<IReadOnlyList<Platform.API.Models.BibleVersionSummary>> GetVersionsAsync(
            string languageRange = "en", CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Platform.API.Models.BibleVersionSummary>>([]);
    }
}
