using Microsoft.AspNetCore.Components;

using Platform.API.Models;

namespace PlatformTestApp.Components.Pages
{
    public partial class CustomReader
    {
        #region State and services
        [SupplyParameterFromQuery(Name = "oauth_error")]
        public string? OAuthError { get; set; }

        private Passage? _passage;
        private bool _loading;
        private string? _error;
        private CancellationTokenSource? _cts;


        private bool CanRead =>
            State.SelectedVersion is not null &&
            State.SelectedBook is not null &&
            State.SelectedChapter is not null &&
            State.SelectedVerseStart is not null;
        #endregion

        protected override void OnInitialized()
            => State.OnStateChanged += OnStateChangedHandler;

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
