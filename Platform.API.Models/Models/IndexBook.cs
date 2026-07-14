using System.Text.Json.Serialization;

namespace Platform.API.Models;

/// <summary>
/// A single book's structural position within a Bible version's index, as returned by
/// <c>GET /v1/bibles/{id}/index</c>. Unlike <see cref="Book"/>, this reflects the real,
/// per-version chapter and verse structure rather than a generic approximation.
/// </summary>
public sealed record IndexBook
{
    /// <summary>Gets the USFM book code (e.g. <c>GEN</c>).</summary>
    [JsonPropertyName("id")]
    public string Usfm { get; init; } = string.Empty;

    /// <summary>Gets the book's short title (e.g. <c>Genesis</c>).</summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the book's full title (e.g. <c>The Book of Genesis</c>).</summary>
    [JsonPropertyName("full_title")]
    public string FullTitle { get; init; } = string.Empty;

    /// <summary>Gets the book's abbreviation (e.g. <c>Gen.</c>).</summary>
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; init; } = string.Empty;

    /// <summary>Gets the scriptural canon this book belongs to.</summary>
    [JsonPropertyName("canon")]
    public BookCanon Canon { get; init; }

    /// <summary>Gets every chapter in this book, in order.</summary>
    [JsonPropertyName("chapters")]
    public IReadOnlyList<IndexChapter> Chapters { get; init; } = [];

    /// <summary>Gets the book's introduction section, if the version provides one.</summary>
    [JsonPropertyName("intro")]
    public IndexSection? Intro { get; init; }
}
