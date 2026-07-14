using System.Text.Json.Serialization;

namespace Platform.API.Models;

/// <summary>
/// The full book/chapter/verse structure for a Bible version, as returned by
/// <c>GET /v1/bibles/{id}/index</c>. This is the authoritative, per-version source for
/// book, chapter, and verse counts — unlike <see cref="Book"/>/<see cref="Chapter"/>, which
/// may be derived from generic approximations elsewhere in the SDK.
/// </summary>
public sealed record BibleIndex
{
    /// <summary>Gets the text direction for this version's script (e.g. <c>ltr</c>, <c>rtl</c>).</summary>
    [JsonPropertyName("text_direction")]
    public string TextDirection { get; init; } = string.Empty;

    /// <summary>Gets every book in this version, in canonical order.</summary>
    [JsonPropertyName("books")]
    public IReadOnlyList<IndexBook> Books { get; init; } = [];
}
