using System.Text.Json.Serialization;

namespace Platform.API.Models;

/// <summary>
/// A Bible passage returned by the YouVersion Platform API.
/// Always display <see cref="Reference"/> and the containing version's copyright alongside the content.
/// </summary>
public sealed record Passage
{
    /// <summary>Gets the USFM passage identifier (e.g. <c>JHN.3.16</c>, <c>GEN.1.1-3</c>).</summary>
    /// <remarks>
    /// This is a normalized, validated USFM passage reference from the YouVersion Platform API.
    /// All USFM references passed to passage operations are validated against
    /// YouVersion.UsfmReferences.BookCatalog before being sent to the API.
    /// </remarks>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the passage content. When <see cref="PassageFormat.Text"/> was requested this is plain
    /// text; when <see cref="PassageFormat.Html"/> was requested this is an HTML fragment
    /// suitable for rendering with the YouVersion Bible stylesheet.
    /// </summary>
    /// <value>The passage content in the requested format.</value>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    /// <summary>Gets the human-readable reference label (e.g. <c>John 3:16</c>).</summary>
    /// <value>The human-readable reference label.</value>
    [JsonPropertyName("reference")]
    public string Reference { get; init; } = string.Empty;
}
