using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Platform.API.Models;

/// <summary>
/// Full metadata for a single Bible version, including its available books.
/// </summary>
public sealed record BibleVersion
{
    /// <summary>Gets the USFM passage identifier (e.g. <c>JHN.3.16</c>, <c>GEN.1.1-3</c>).</summary>
    /// <remarks>This value is normalized and validated by YouVersion.UsfmReferences before being sent to the API.</remarks>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>Gets the short abbreviation for the version (e.g. <c>NIV</c>, <c>BSB</c>).</summary>
    /// <value>The short version abbreviation.</value>
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; init; } = string.Empty;

    /// <summary>Gets the abbreviation localized to the version's language.</summary>
    /// <value>The localized version abbreviation.</value>
    [JsonPropertyName("localized_abbreviation")]
    public string LocalizedAbbreviation { get; init; } = string.Empty;

    /// <summary>Gets the full title of the Bible version.</summary>
    /// <value>The full title of the Bible version.</value>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the title localized to the version's language.</summary>
    /// <value>The localized title of the Bible version.</value>
    [JsonPropertyName("localized_title")]
    public string LocalizedTitle { get; init; } = string.Empty;

    /// <summary>Gets the BCP-47 language tag (e.g. <c>en</c>, <c>es</c>).</summary>
    /// <value>The BCP-47 language tag for this version.</value>
    [JsonPropertyName("language_tag")]
    public string LanguageTag { get; init; } = string.Empty;

    /// <summary>Gets the copyright statement to display alongside any Bible text from this version.</summary>
    /// <value>The copyright statement for this Bible version.</value>
    [JsonPropertyName("copyright")]
    public string Copyright { get; init; } = string.Empty;

    /// <summary>Gets the short promotional description of the version.</summary>
    /// <value>The promotional description, or <see langword="null"/> if none is available.</value>
    [JsonPropertyName("promotional_content")]
    public string? PromotionalContent { get; init; }

    /// <summary>Gets the URL to the publisher's website.</summary>
    /// <value>The publisher URL, or <see langword="null"/> if none is available.</value>
    [JsonPropertyName("publisher_url")]
    public string? PublisherUrl { get; init; }

    /// <summary>
    /// Gets the USFM book codes available in this version (e.g. <c>GEN</c>, <c>JHN</c>).
    /// </summary>
    /// <value>The list of USFM book codes available in this version.</value>
    [JsonPropertyName("books")]
    public IReadOnlyList<string> Books { get; init; } = [];

    /// <summary>Gets the deep link to this version on YouVersion (bible.com).</summary>
    /// <value>The YouVersion deep link URL, or <see langword="null"/> if none is available.</value>
    [JsonPropertyName("youversion_deep_link")]
    public string? YouVersionDeepLink { get; init; }
}
