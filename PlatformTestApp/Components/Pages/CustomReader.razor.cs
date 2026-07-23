using Microsoft.AspNetCore.Components;

using Platform.API.Models;

namespace PlatformTestApp.Components.Pages
{
    public partial class CustomReader
    {
        #region State and services
        [SupplyParameterFromQuery(Name = "oauth_error")]
        public string? OAuthError { get; set; }

        // Only present on the single redirect immediately following an OAuth/Data Exchange callback.
        // On every other page load this is null, so the persisted HighlightsPermissionStore is the
        // source of truth for whether highlighting stays enabled across reloads and revisits.
        [SupplyParameterFromQuery(Name = "highlights")]
        public string? HighlightsStatus { get; set; }

        private Passage? _passage;
        private bool _loading;
        private string? _error;
        private CancellationTokenSource? _cts;
        private bool _highlightsEnabled;
        private bool _isSignedIn;


        private bool CanRead =>
            State.SelectedVersion is not null &&
            State.SelectedBook is not null &&
            State.SelectedChapter is not null &&
            State.SelectedVerseStart is not null;
        #endregion

        protected override async Task OnInitializedAsync()
        {
            State.OnStateChanged += OnStateChangedHandler;

            _highlightsEnabled = HighlightsStatus switch
            {
                "granted" => true,
                "denied" => false,
                _ => await PermissionStore.GetGrantedAsync()
            };
            _isSignedIn = await CheckSignedInAsync();
        }

        // Re-check after first interactive render — a permission grant or token persisted during the
        // OAuth callback round-trip may not be visible in the SSR prerender scope OnInitializedAsync
        // ran in (same caveat VerseComponent handles for its own sign-in check).
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;

            var signedIn = await CheckSignedInAsync();
            var granted = HighlightsStatus is null ? await PermissionStore.GetGrantedAsync() : _highlightsEnabled;

            if (signedIn == _isSignedIn && granted == _highlightsEnabled) return;

            _isSignedIn = signedIn;
            _highlightsEnabled = granted;
            StateHasChanged();
        }

        private async Task<bool> CheckSignedInAsync()
            => (await AuthSessionService.GetCurrentSessionAsync()).IsSignedIn;

        // A plain <a href> here gets swallowed by Blazor's enhanced navigation, which fetches the
        // response and tries to diff it into the page instead of following the server redirect to
        // the platform's off-site approval page. forceLoad: true bypasses that, same as BibleAuth's
        // sign-in/out buttons.
        private void RequestHighlightsAsync() => Nav.NavigateTo("/auth/request-highlights", forceLoad: true);

        private void OnStateChangedHandler()
        {
            _passage = null;
            _error = null;
            InvokeAsync(StateHasChanged);
        }

        private async Task ReadAsync()
        {
            if (!CanRead) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _loading = true;
            _error = null;
            _passage = null;

            try
            {
                _passage = await PassageService.GetPassageAsync(
                    State.SelectedVersion!.Id,
                    State.SelectedBook!.Usfm,
                    State.SelectedChapter!.Value,
                    State.SelectedVerseStart!.Value,
                    State.SelectedVerseEnd,
                    new PassageRequestOptions { Format = PassageFormat.Html },
                    _cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
            finally
            {
                _loading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        public void Dispose()
        {
            State.OnStateChanged -= OnStateChangedHandler;
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
