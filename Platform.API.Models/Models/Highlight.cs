using System.Text.Json.Serialization;

namespace Platform.API.Models;

/// <summary>
/// A Bible verse highlight associated with a user account.
/// </summary>
/// <remarks>
/// Highlights have no opaque identifier from the API: a highlight is identified by the
/// combination of <see cref="BibleId"/> and <see cref="PassageId"/>. Creating a highlight for a
/// passage that already has one updates its color in place ("create or update").
/// </remarks>
public sealed record Highlight
{
    /// <summary>Gets the Bible version identifier this highlight belongs to.</summary>
    /// <value>The numeric Bible version identifier.</value>
    [JsonPropertyName("bible_id")]
    public int BibleId { get; init; }

    /// <summary>Gets the USFM identifier of the highlighted passage (e.g. <c>JHN.3.16</c>).</summary>
    [JsonPropertyName("passage_id")]
    public string PassageId { get; init; } = string.Empty;

    /// <summary>Gets the highlight color as a hex string (e.g. <c>44aa44</c>), without a leading <c>#</c>.</summary>
    [JsonPropertyName("color")]
    public string Color { get; init; } = string.Empty;
}
