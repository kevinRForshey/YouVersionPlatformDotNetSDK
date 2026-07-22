using Microsoft.AspNetCore.Components;

namespace Platform.SDK.Components.BibleComponents
{
    /// <summary>Dropdown for selecting a chapter within the currently selected book.</summary>
    public partial class ChapterPicker
    {
        /// <inheritdoc/>
        protected override void OnInitialized()
       => State.OnStateChanged += OnStateChangedHandler;

        private void OnStateChangedHandler()
            => InvokeAsync(StateHasChanged);

        private void OnChapterChanged(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out var ch))
                State.SelectChapter(ch);
        }

        /// <inheritdoc/>
        public void Dispose()
            => State.OnStateChanged -= OnStateChangedHandler;
    }
}