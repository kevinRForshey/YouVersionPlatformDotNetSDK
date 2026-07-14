using Microsoft.AspNetCore.Components;

using Platform.API.Models;

namespace Platform.SDK.Components.BibleComponents
{
    public partial class VersionPicker
    {
        /// <summary>
        /// BCP-47 language range used to filter the version list.
        /// Passed down from <see cref="BibleReader.LanguageRange"/>. Defaults to "en".
        /// </summary>
        [Parameter] public string LanguageRange { get; set; } = "en";

        private IReadOnlyList<BibleVersionSummary> _versions = [];
        private BibleVersionSummary? _selected;
        private bool _loading = true;
        private string? _error;
        private string? _loadedForLanguage;

        protected override async Task OnParametersSetAsync()
        {
            // Only reload when the language actually changes — guards against
            // re-fetching on every parent render cycle.
            if (LanguageRange == _loadedForLanguage) return;

            _loading = true;
            _error = null;
            _loadedForLanguage = LanguageRange;

            try
            {
                _versions = await VersionService.GetVersionsAsync(LanguageRange);
            }
            catch (Exception ex)
            {
                _error = $"Could not load Bible versions: {ex.Message}";
            }
            finally
            {
                _loading = false;
            }
        }

        private void OnVersionChanged(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out var id))
            {
                _selected = _versions.FirstOrDefault(v => v.Id == id);
                if (_selected is not null)
                    State.SelectVersion(_selected);
            }
            else
            {
                _selected = null;
                State.Reset();
            }
        }
    }
}
