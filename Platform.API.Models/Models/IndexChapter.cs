using System.Text.Json.Serialization;

using Platform.API.Models.Json;

namespace Platform.API.Models;

/// <summary>
/// A single chapter's structural position within a Bible version's index, as returned by
/// <c>GET /v1/bibles/{id}/index</c>.
/// </summary>
public sealed record IndexChapter
{
    /// <summary>Gets the chapter number within its book.</summary>
    [JsonPropertyName("id")]
    public int Number { get; init; }

    /// <summary>Gets the USFM passage identifier (e.g. <c>GEN.1</c>).</summary>
    [JsonPropertyName("passage_id")]
    public string Usfm { get; init; } = string.Empty;

    /// <summary>Gets the chapter's display title (typically its number, as a string).</summary>
    [JsonPropertyName("title")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets every verse in this chapter, in order.</summary>
    [JsonPropertyName("verses")]
    public IReadOnlyList<IndexVerse> Verses { get; init; } = [];
}
