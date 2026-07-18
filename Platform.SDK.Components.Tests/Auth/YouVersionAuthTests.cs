using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Platform.SDK.Components.Auth;
using Platform.SDK.Services;
using Xunit;

namespace Platform.SDK.Components.Tests.Auth;

public sealed class YouVersionAuthTests : TestContext
{
    private readonly Mock<IAuthSessionService> _authSessionService = new();

    private IRenderedComponent<YouVersionAuth> RenderAuth(Action<ComponentParameterCollectionBuilder<YouVersionAuth>>? configure = null)
    {
        Services.AddSingleton(_authSessionService.Object);
        return configure is null ? RenderComponent<YouVersionAuth>() : RenderComponent<YouVersionAuth>(configure);
    }

    [Fact]
    public void NoToken_ShowsSignInButton()
    {
        _authSessionService.Setup(s => s.GetCurrentSessionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AuthSession.SignedOut);

        var cut = RenderAuth();

        cut.Markup.Should().Contain("Sign in");
        cut.Markup.Should().NotContain("Sign out");
    }

    [Fact]
    public void ValidNonExpiredToken_ShowsUserNameAndSignOutButton()
    {
        _authSessionService
            .Setup(s => s.GetCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthSession(IsSignedIn: true, DisplayName: "Jane Doe"));

        var cut = RenderAuth();

        cut.Markup.Should().Contain("Jane Doe");
        cut.Markup.Should().Contain("Sign out");
    }

    [Fact]
    public void ExpiredToken_ShowsSignInButton()
    {
        _authSessionService.Setup(s => s.GetCurrentSessionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AuthSession.SignedOut);

        var cut = RenderAuth();

        cut.Markup.Should().Contain("Sign in");
    }

    [Fact]
    public void OAuthError_RendersDangerAlert()
    {
        _authSessionService.Setup(s => s.GetCurrentSessionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AuthSession.SignedOut);

        var cut = RenderAuth(p => p.Add(x => x.OAuthError, "invalid_grant"));

        cut.Markup.Should().Contain("alert-danger").And.Contain("invalid_grant");
    }

    [Fact]
    public void SignInClick_WithDelegate_InvokesDelegateInsteadOfNavigating()
    {
        _authSessionService.Setup(s => s.GetCurrentSessionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AuthSession.SignedOut);
        var invoked = false;

        var cut = RenderAuth(p => p.Add(x => x.OnSignInRequested, EventCallback.Factory.Create(this, () => invoked = true)));
        cut.Find("button").Click();

        invoked.Should().BeTrue();
    }

    [Fact]
    public void SignOutClick_WithDelegate_InvokesDelegateInsteadOfNavigating()
    {
        _authSessionService
            .Setup(s => s.GetCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthSession(IsSignedIn: true, DisplayName: null));
        var invoked = false;

        var cut = RenderAuth(p => p.Add(x => x.OnSignOutRequested, EventCallback.Factory.Create(this, () => invoked = true)));
        cut.Find("button").Click();

        invoked.Should().BeTrue();
    }

    [Fact]
    public void SignInClick_WithoutDelegate_NavigatesToLoginPath()
    {
        _authSessionService.Setup(s => s.GetCurrentSessionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AuthSession.SignedOut);

        var cut = RenderAuth(p => p.Add(x => x.LoginPath, "/custom/login"));

        cut.Find("button").Click();

        Services.GetRequiredService<NavigationManager>().Uri.Should().Contain("/custom/login");
    }
}
