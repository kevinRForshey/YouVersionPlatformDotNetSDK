using System.Text;
using System.Text.Json;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Platform.API.OAuth;
using Platform.SDK.Components.Auth;
using Xunit;

namespace Platform.SDK.Components.Tests.Auth;

public sealed class YouVersionAuthTests : TestContext
{
    private readonly Mock<ITokenProvider> _tokenProvider = new();

    private static string MakeIdToken(string name)
    {
        string Segment(object payload) => Convert.ToBase64String(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"{Segment(new { alg = "none" })}.{Segment(new { name })}.";
    }

    private IRenderedComponent<YouVersionAuth> RenderAuth(Action<ComponentParameterCollectionBuilder<YouVersionAuth>>? configure = null)
    {
        Services.AddSingleton(_tokenProvider.Object);
        return configure is null ? RenderComponent<YouVersionAuth>() : RenderComponent<YouVersionAuth>(configure);
    }

    [Fact]
    public void NoToken_ShowsSignInButton()
    {
        _tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((OAuthTokenResponse?)null);

        var cut = RenderAuth();

        cut.Markup.Should().Contain("Sign in");
        cut.Markup.Should().NotContain("Sign out");
    }

    [Fact]
    public void ValidNonExpiredToken_ShowsUserNameAndSignOutButton()
    {
        var token = new OAuthTokenResponse
        {
            AccessToken = "access-token",
            IdToken = MakeIdToken("Jane Doe"),
            ExpiresIn = 3600,
            ReceivedAt = DateTimeOffset.UtcNow,
        };
        _tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var cut = RenderAuth();

        cut.Markup.Should().Contain("Jane Doe");
        cut.Markup.Should().Contain("Sign out");
    }

    [Fact]
    public void ExpiredToken_ShowsSignInButton()
    {
        var token = new OAuthTokenResponse
        {
            AccessToken = "access-token",
            IdToken = MakeIdToken("Jane Doe"),
            ExpiresIn = 60,
            ReceivedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };
        _tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var cut = RenderAuth();

        cut.Markup.Should().Contain("Sign in");
    }

    [Fact]
    public void OAuthError_RendersDangerAlert()
    {
        _tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((OAuthTokenResponse?)null);

        var cut = RenderAuth(p => p.Add(x => x.OAuthError, "invalid_grant"));

        cut.Markup.Should().Contain("alert-danger").And.Contain("invalid_grant");
    }

    [Fact]
    public void SignInClick_WithDelegate_InvokesDelegateInsteadOfNavigating()
    {
        _tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((OAuthTokenResponse?)null);
        var invoked = false;

        var cut = RenderAuth(p => p.Add(x => x.OnSignInRequested, EventCallback.Factory.Create(this, () => invoked = true)));
        cut.Find("button").Click();

        invoked.Should().BeTrue();
    }

    [Fact]
    public void SignOutClick_WithDelegate_InvokesDelegateInsteadOfNavigating()
    {
        var token = new OAuthTokenResponse
        {
            AccessToken = "access-token",
            ExpiresIn = 3600,
            ReceivedAt = DateTimeOffset.UtcNow,
        };
        _tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(token);
        var invoked = false;

        var cut = RenderAuth(p => p.Add(x => x.OnSignOutRequested, EventCallback.Factory.Create(this, () => invoked = true)));
        cut.Find("button").Click();

        invoked.Should().BeTrue();
    }

    [Fact]
    public void SignInClick_WithoutDelegate_NavigatesToLoginPath()
    {
        _tokenProvider.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((OAuthTokenResponse?)null);

        var cut = RenderAuth(p => p.Add(x => x.LoginPath, "/custom/login"));

        cut.Find("button").Click();

        Services.GetRequiredService<NavigationManager>().Uri.Should().Contain("/custom/login");
    }
}
