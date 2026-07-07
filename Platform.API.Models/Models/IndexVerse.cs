using System.Text.Json.Serialization;

using Platform.API.Models.Json;

namespace Platform.API.Models;

/// <summary>
/// A single verse's structural position within a Bible version's index, as returned by
/// <c>GET /v1/bibles/{id}/index</c>. Carries no scripture text — use <c>IPassageClient</c>
/// to fetch verse content.
/// </summary>
public sealed record IndexVerse
{
    /// <summary>Gets the verse number within its chapter.</summary>
    [JsonPropertyName("id")]
    public int Number { get; init; }

    /// <summary>Gets the USFM passage identifier (e.g. <c>GEN.1.1</c>).</summary>
    [JsonPropertyName("passage_id")]
    public string Usfm { get; init; } = string.Empty;

    /// <summary>Gets the verse's display title (typically its number, as a string).</summary>
    [JsonPropertyName("title")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string Title { get; init; } = string.Empty;
}
