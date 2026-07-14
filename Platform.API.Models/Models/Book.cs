using System.Text.Json.Serialization;

namespace Platform.API.Models;

/// <summary>
/// Represents a book of the Bible within a specific version.
/// </summary>
public sealed record Book
{
    /// <summary>Gets the USFM book code (e.g. <c>GEN</c>, <c>MAT</c>, <c>REV</c>).</summary>
    /// <remarks>
    /// This is a normalized, validated USFM book code from the YouVersion Platform API.
    /// All USFM references passed to passage and highlight operations are validated against
    /// YouVersion.UsfmReferences.BookCatalog before being sent to the API.
    /// </remarks>
    [JsonPropertyName("usfm")]
    public string Usfm { get; init; } = string.Empty;

    /// <summary>Gets the human-readable name of the book (e.g. <c>Genesis</c>).</summary>
    /// <value>The human-readable book name.</value>
    [JsonPropertyName("human")]
    public string Human { get; init; } = string.Empty;

    /// <summary>Gets the number of chapters in this book for the given Bible version.</summary>
    /// <value>The chapter count for this book.</value>
    [JsonPropertyName("chapters")]
    public int ChapterCount { get; init; }
}
