#region usings
using Microsoft.AspNetCore.Components;
using Platform.API.Models;
using Platform.SDK.Services;
#endregion

namespace Platform.SDK.Components.BibleComponents
{
    public partial class BibleReader : IDisposable
    {
        // ── Injected services ────────────────────────────────────────────────
        [Inject] private IBibleReaderStateService State { get; set; } = default!;
        [Inject] private IPassageService PassageService { get; set; } = default!;

        // ── Customisation parameters ─────────────────────────────────────────

        /// <summary>Heading displayed at the top of the component. Defaults to "Bible Reader".</summary>
        [Parameter] public string Title { get; set; } = "Bible Reader";

        /// <summary>
        /// BCP-47 language range used to filter the Bible version list (e.g. "en", "es", "fr").
        /// Cascades down to <see cref="VersionPicker"/>. Defaults to English.
        /// </summary>
        [Parameter] public string LanguageRange { get; set; } = "en";

        /// <summary>
        /// Passage format requested from the API. Defaults to <see cref="PassageFormat.Html"/>.
        /// Use <see cref="PassageFormat.Text"/> to receive plain text suitable for display or processing.
        /// </summary>
        [Parameter] public PassageFormat Format { get; set; } = PassageFormat.Html;

        /// <summary>
        /// Fired after a passage is successfully loaded.
        /// Use this to update parent state, log analytics, copy text, etc.
        /// </summary>
        [Parameter] public EventCallback<Passage> OnPassageLoaded { get; set; }

        /// <summary>
        /// Optional custom rendering for the loaded passage.
        /// The <see cref="Passage"/> is passed as the render-fragment context.
        /// When omitted the built-in <c>VerseComponent</c> is used.
        /// </summary>
        [Parameter] public RenderFragment<Passage>? PassageTemplate { get; set; }

        /// <summary>
        /// Invoked when the user clicks "Sign in".
        /// When no delegate is provided the embedded <see cref="Platform.SDK.Components.Auth.YouVersionAuth"/> falls back to navigating to <see cref="LoginPath"/>.
        /// </summary>
        [Parameter] public EventCallback OnSignInRequested { get; set; }

        /// <summary>
        /// Invoked when the user clicks "Sign out".
        /// When no delegate is provided the embedded <see cref="Platform.SDK.Components.Auth.YouVersionAuth"/> falls back to navigating to <see cref="LogoutPath"/>.
        /// </summary>
        [Parameter] public EventCallback OnSignOutRequested { get; set; }

        /// <summary>Sign-in route forwarded to the embedded <see cref="Platform.SDK.Components.Auth.YouVersionAuth"/>. Defaults to "/auth/login".</summary>
        [Parameter] public string LoginPath { get; set; } = "/auth/login";

        /// <summary>Sign-out route forwarded to the embedded <see cref="Platform.SDK.Components.Auth.YouVersionAuth"/>. Defaults to "/auth/logout".</summary>
        [Parameter] public string LogoutPath { get; set; } = "/auth/logout";

        /// <summary>
        /// OAuth error message forwarded to the embedded <see cref="Platform.SDK.Components.Auth.YouVersionAuth"/>
        /// (e.g. from the host page's <c>?oauth_error=</c> query parameter).
        /// </summary>
        [Parameter] public string? OAuthError { get; set; }

        /// <summary>
        /// Fired after the user creates or updates a highlight from the default (non-templated)
        /// passage display. Has no effect when a custom <see cref="PassageTemplate"/> is supplied.
        /// </summary>
        [Parameter] public EventCallback<Highlight> OnHighlightCreated { get; set; }

        /// <summary>
        /// Fired after the user removes a highlight from the default (non-templated) passage
        /// display. Has no effect when a custom <see cref="PassageTemplate"/> is supplied.
        /// </summary>
        [Parameter] public EventCallback<Highlight> OnHighlightCleared { get; set; }

        /// <summary>
        /// Whether the default (non-templated) passage display shows the highlighting toolbar and
        /// verse interactions. Defaults to <see langword="true"/>. Has no effect when a custom
        /// <see cref="PassageTemplate"/> is supplied.
        /// </summary>
        [Parameter] public bool EnableHighlighting { get; set; } = true;

        // ── Private state ────────────────────────────────────────────────────
        private Passage? _passage;
        private bool _loading;
        private string? _error;
        private CancellationTokenSource? _cts;

        private string? _copyright;
        private int _versionId;

        // ── Lifecycle ────────────────────────────────────────────────────────
        protected override Task OnInitializedAsync()
        {
            State.OnStateChanged += OnStateChangedHandler;
            return Task.CompletedTask;
        }

        // ── Reading ──────────────────────────────────────────────────────────
        private bool CanRead =>
            State.SelectedVersion is not null &&
            State.SelectedBook is not null &&
            State.SelectedChapter is not null &&
            State.SelectedVerseStart is not null;

        private async Task ReadPassageAsync()
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
                    new PassageRequestOptions { Format = Format },
                    _cts.Token);

                _copyright = State.SelectedVersion.Copyright;
                _versionId = State.SelectedVersion.Id;

                if (OnPassageLoaded.HasDelegate)
                    await OnPassageLoaded.InvokeAsync(_passage);
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer request — ignore.
            }
            catch (Exception ex)
            {
                _error = $"Could not load passage: {ex.Message}";
            }
            finally
            {
                _loading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        // ── State change ─────────────────────────────────────────────────────
        private void OnStateChangedHandler()
            => InvokeAsync(() =>
            {
                _passage = null;
                _error = null;
                StateHasChanged();
            });

        public void Dispose()
        {
            State.OnStateChanged -= OnStateChangedHandler;
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
