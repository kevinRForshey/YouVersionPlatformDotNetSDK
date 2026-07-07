using System.Text.Json.Serialization;

using Platform.API.Models.Json;

namespace Platform.API.Models;

/// <summary>
/// A named, non-chapter section of a book in a Bible version's index (e.g. a book introduction).
/// </summary>
public sealed record IndexSection
{
    /// <summary>Gets the section's identifier (e.g. <c>INTRO</c>).</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the USFM passage identifier (e.g. <c>GEN.INTRO</c>).</summary>
    [JsonPropertyName("passage_id")]
    public string Usfm { get; init; } = string.Empty;

    /// <summary>Gets the section's display title (e.g. <c>Intro</c>).</summary>
    [JsonPropertyName("title")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Title { get; init; } = string.Empty;
}
