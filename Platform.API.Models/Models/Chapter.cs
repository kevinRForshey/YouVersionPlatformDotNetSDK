using System.Text.Json.Serialization;

namespace Platform.API.Models;

/// <summary>
/// Represents a chapter within a book of the Bible.
/// </summary>
public sealed record Chapter
{
    /// <summary>Gets the USFM chapter identifier (e.g. <c>GEN.1</c>).</summary>
    [JsonPropertyName("usfm")]
    public string Usfm { get; init; } = string.Empty;

    /// <summary>Gets the human-readable chapter reference (e.g. <c>Genesis 1</c>).</summary>
    /// <value>The human-readable chapter reference.</value>
    [JsonPropertyName("human")]
    public string Human { get; init; } = string.Empty;

    /// <summary>Gets the number of verses in this chapter for the given Bible version.</summary>
    /// <value>The verse count for this chapter.</value>
    [JsonPropertyName("verses")]
    public int VerseCount { get; init; }
}
