using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Platform.API.Exceptions;
using Platform.API.Models;
using Platform.API.OAuth;
using Platform.SDK.Services;
using YouVersion.UsfmReferences;

namespace Platform.SDK.Components.BibleComponents.Verses;

public partial class VerseComponent : ComponentBase
{
    private sealed record HighlightColorOption(string Name, string Hex);

    // A small default palette. Colors are hex strings without a leading '#', matching the API's
    // highlight color format, so the armed color can be sent straight through to the service.
    private static readonly HighlightColorOption[] AllColors =
    [
        new("Yellow", "ffd54f"),
        new("Green", "81c784"),
        new("Blue", "64b5f6"),
        new("Orange", "ff8a65"),
        new("Pink", "f06292"),
        new("Purple", "ba68c8"),
    ];

    // Matches the verse-start marker pair the YouVersion HTML passage format emits immediately
    // before each verse's text: <span class="yv-v" v="16"></span><span class="yv-vlbl">16</span>.
    private static readonly Regex VerseMarkerRegex = new(
        """<span class="yv-v" v="(\d+)"[^>]*></span>(?:<span class="yv-vlbl"[^>]*>\d+</span>)?""",
        RegexOptions.Compiled);

    // Structural wrapper divs carry no per-verse meaning. They're stripped before splitting so the
    // last verse segment doesn't end up with unbalanced closing tags once the content is split apart.
    private static readonly Regex WrapperDivRegex = new("""</?div[^>]*>""", RegexOptions.Compiled);

    [Inject] private IHighlightService HighlightService { get; set; } = default!;
    [Inject] private ITokenProvider TokenProvider { get; set; } = default!;

    [Parameter, EditorRequired]
    public Passage Passage { get; set; } = default!;

    [Parameter]
    public string? Copyright { get; set; }

    /// <summary>
    /// The Bible version id the passage was read from. Required to create or look up highlights;
    /// when omitted (0) the highlighting toolbar is hidden and the passage renders read-only.
    /// </summary>
    [Parameter]
    public int VersionId { get; set; }

    /// <summary>
    /// Whether the highlighting toolbar and verse interactions are enabled. Defaults to
    /// <see langword="true"/>. Set to <see langword="false"/> to render the passage strictly
    /// read-only — no sign-in check and no highlight API calls are made at all.
    /// </summary>
    [Parameter]
    public bool EnableHighlighting { get; set; } = true;

    /// <summary>Fired after the user creates or updates a highlight by clicking an armed verse.</summary>
    [Parameter]
    public EventCallback<Highlight> OnHighlightCreated { get; set; }

    /// <summary>Fired after the user removes a highlight by double-clicking it.</summary>
    [Parameter]
    public EventCallback<Highlight> OnHighlightCleared { get; set; }

    private sealed record VerseSegment(int Number, string Html);

    private string? _lastPassageKey;
    private IReadOnlyList<VerseSegment> _segments = [];
    private Reference? _passageReference;
    private string? _armedColor;
    private Dictionary<int, Highlight> _highlightsByVerse = [];
    private int? _savingVerse;
    private string? _highlightError;
    private bool _isSignedIn;

    // Whether the passage is structurally eligible for highlighting (enabled, with a version and
    // a parseable reference present), independent of whether the current user is signed in.
    private bool CanShowHighlightUi => EnableHighlighting && VersionId > 0 && _passageReference is not null;

    // Whether highlight reads/writes can actually be attempted — highlights are per-user data,
    // so the API requires a signed-in OAuth session for both loading and saving them.
    private bool CanHighlight => CanShowHighlightUi && _isSignedIn;

    protected override async Task OnInitializedAsync()
    {
        if (EnableHighlighting)
            await CheckSignInAsync();
    }

    // Re-check after first interactive render — a token stored during the OAuth callback
    // round-trip may not be visible in the SSR prerender scope that OnInitializedAsync ran in.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && EnableHighlighting)
            await CheckSignInAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        var passageKey = $"{VersionId}:{Passage.Id}";
        if (_lastPassageKey == passageKey) return;
        _lastPassageKey = passageKey;

        _highlightError = null;
        _armedColor = null;
        _passageReference = TryParseReference(Passage.Id);
        _segments = SplitIntoVerses(Passage.Content);

        await LoadHighlightsAsync();
    }

    private async Task CheckSignInAsync()
    {
        var token = await TokenProvider.GetTokenAsync();
        var signedIn = token is not null && !token.IsExpired();

        if (signedIn == _isSignedIn) return;

        _isSignedIn = signedIn;
        if (signedIn)
            await LoadHighlightsAsync();

        await InvokeAsync(StateHasChanged);
    }

    private static Reference? TryParseReference(string usfm)
    {
        try
        {
            return Reference.FromString(usfm);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static IReadOnlyList<VerseSegment> SplitIntoVerses(string content)
    {
        var flattened = WrapperDivRegex.Replace(content, string.Empty);
        var matches = VerseMarkerRegex.Matches(flattened);
        if (matches.Count == 0) return [];

        var segments = new List<VerseSegment>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : flattened.Length;
            var verseNumber = int.Parse(matches[i].Groups[1].Value);
            segments.Add(new VerseSegment(verseNumber, flattened[start..end]));
        }

        return segments;
    }

    private async Task LoadHighlightsAsync()
    {
        _highlightsByVerse = [];
        if (!CanHighlight) return;

        try
        {
            // One call for the whole chapter: the API returns one entry per highlighted verse.
            var chapterReference = _passageReference!.ToChapterOrIntro();
            var highlights = await HighlightService.GetHighlightsAsync(VersionId, chapterReference);

            foreach (var highlight in highlights)
            {
                if (TryParseReference(highlight.PassageId) is not { } highlightReference) continue;
                if (highlightReference.Book != _passageReference.Book ||
                    highlightReference.Chapter != _passageReference.Chapter) continue;

                foreach (var range in highlightReference.Verses)
                    for (var verse = range.Start; verse <= range.End; verse++)
                        _highlightsByVerse[verse] = highlight;
            }
        }
        catch (Exception ex)
        {
            _highlightError = DescribeError(ex, "Could not load existing highlights");
        }
    }

    private void ToggleArmedColor(string hex)
        => _armedColor = _armedColor == hex ? null : hex;

    // The API can 401 even when our local expiry check thought the token was still valid — either
    // the token was revoked server-side, or the user never granted the separate "highlights" Data
    // Exchange permission (sign-in alone only grants identity). This is defense-in-depth alongside
    // the sign-in gating on CanHighlight, not a substitute for it.
    private static string DescribeError(Exception ex, string fallbackPrefix) =>
        ex is YouVersionApiException { StatusCode: HttpStatusCode.Unauthorized }
            ? "Highlights access isn't available. Please sign in and grant highlights permission when prompted."
            : $"{fallbackPrefix}: {ex.Message}";

    private async Task OnVerseClickAsync(int verseNumber)
    {
        if (!CanHighlight) return;
        if (_armedColor is not { } color) return;
        if (_highlightsByVerse.ContainsKey(verseNumber)) return;

        _savingVerse = verseNumber;
        _highlightError = null;
        StateHasChanged();

        try
        {
            var verseReference = new Reference(
                book: _passageReference!.Book,
                chapter: _passageReference.Chapter,
                verses: [new VerseRange(verseNumber, verseNumber)]);

            var saved = await HighlightService.CreateOrUpdateHighlightAsync(VersionId, verseReference, color);
            _highlightsByVerse[verseNumber] = saved;

            if (OnHighlightCreated.HasDelegate)
                await OnHighlightCreated.InvokeAsync(saved);
        }
        catch (Exception ex)
        {
            _highlightError = DescribeError(ex, "Could not save highlight");
        }
        finally
        {
            _savingVerse = null;
            StateHasChanged();
        }
    }

    private async Task OnVerseDblClickAsync(int verseNumber)
    {
        if (!CanHighlight) return;
        if (!_highlightsByVerse.TryGetValue(verseNumber, out var highlight)) return;

        _savingVerse = verseNumber;
        _highlightError = null;
        StateHasChanged();

        try
        {
            var verseReference = new Reference(
                book: _passageReference!.Book,
                chapter: _passageReference.Chapter,
                verses: [new VerseRange(verseNumber, verseNumber)]);

            await HighlightService.ClearHighlightsAsync(VersionId, verseReference);
            _highlightsByVerse.Remove(verseNumber);

            if (OnHighlightCleared.HasDelegate)
                await OnHighlightCleared.InvokeAsync(highlight);
        }
        catch (Exception ex)
        {
            _highlightError = DescribeError(ex, "Could not remove highlight");
        }
        finally
        {
            _savingVerse = null;
            StateHasChanged();
        }
    }
}
