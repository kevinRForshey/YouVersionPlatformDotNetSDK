using Microsoft.AspNetCore.Components;
using Platform.SDK.Services;

namespace Platform.SDK.Components.Auth;

/// <summary>
/// Self-contained Platform OAuth widget.
/// Reads the current sign-in state from <see cref="IAuthSessionService"/> and renders
/// sign-in / sign-out controls with the signed-in user's display name.
/// </summary>
/// <remarks>
/// Navigation on sign-in and sign-out is delegated to
/// <see cref="OnSignInRequested"/> / <see cref="OnSignOutRequested"/> when
/// provided, or falls back to a full-page navigation to
/// <see cref="LoginPath"/> / <see cref="LogoutPath"/>.
/// </remarks>
public partial class BibleAuth
{
    [Inject] private IAuthSessionService AuthSessionService { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    /// <summary>Invoked when the user clicks "Sign in". Falls back to navigating to <see cref="LoginPath"/>.</summary>
    [Parameter] public EventCallback OnSignInRequested { get; set; }

    /// <summary>Invoked when the user clicks "Sign out". Falls back to navigating to <see cref="LogoutPath"/>.</summary>
    [Parameter] public EventCallback OnSignOutRequested { get; set; }

    /// <summary>Sign-in route used when <see cref="OnSignInRequested"/> has no delegate. Defaults to "/auth/login".</summary>
    [Parameter] public string LoginPath { get; set; } = "/auth/login";

    /// <summary>Sign-out route used when <see cref="OnSignOutRequested"/> has no delegate. Defaults to "/auth/logout".</summary>
    [Parameter] public string LogoutPath { get; set; } = "/auth/logout";

    /// <summary>
    /// OAuth error message to surface (e.g. forwarded from a <c>?oauth_error=</c> query parameter).
    /// Renders a danger alert below the sign-in controls when non-null.
    /// </summary>
    [Parameter] public string? OAuthError { get; set; }

    /// <summary>CSS class applied to the sign-in and sign-out buttons. Defaults to Bootstrap's "btn btn-sm btn-outline-primary".</summary>
    [Parameter] public string ButtonCssClass { get; set; } = "btn btn-sm btn-outline-primary";

    /// <summary>CSS class applied to the username span when the user is signed in.</summary>
    [Parameter] public string UsernameCssClass { get; set; } = "me-2";

    private bool _isSignedIn;
    private string? _userName;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
        => await CheckSignInAsync();

    // Re-check after first interactive render — a token stored during the OAuth callback
    // round-trip may not be visible in the SSR prerender scope.
    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await CheckSignInAsync();
    }

    private async Task CheckSignInAsync()
    {
        var session = await AuthSessionService.GetCurrentSessionAsync();

        if (session.IsSignedIn != _isSignedIn || session.DisplayName != _userName)
        {
            _isSignedIn = session.IsSignedIn;
            _userName = session.DisplayName;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task SignInAsync()
    {
        if (OnSignInRequested.HasDelegate)
            await OnSignInRequested.InvokeAsync();
        else
            Nav.NavigateTo(LoginPath, forceLoad: true);
    }

    private async Task SignOutAsync()
    {
        if (OnSignOutRequested.HasDelegate)
            await OnSignOutRequested.InvokeAsync();
        else
            Nav.NavigateTo(LogoutPath, forceLoad: true);
    }
}
