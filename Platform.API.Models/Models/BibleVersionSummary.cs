using System.Text.Json.Serialization;

namespace Platform.API.Models;

/// <summary>
/// A lightweight summary of a Bible version returned by the list-versions endpoint.
/// </summary>
public sealed record BibleVersionSummary
{
    /// <summary>Gets the USFM passage identifier (e.g. <c>JHN.3.16</c>, <c>GEN.1.1-3</c>).</summary>
    /// <remarks>This value is normalized and validated by BiblePlatform.UsfmReferences before being sent to the API.</remarks>
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

    /// <summary>Gets the copyright notice for this version, if provided by the API.</summary>
    [JsonPropertyName("copyright")]
    public string? Copyright { get; init; }
}
