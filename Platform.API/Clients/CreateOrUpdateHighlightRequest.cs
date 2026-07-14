using System.Text.Json.Serialization;

namespace Platform.API.Clients;

/// <summary>
/// Request body sent when creating or updating a highlight via the YouVersion Platform API.
/// </summary>
internal sealed record CreateOrUpdateHighlightRequest
{
    [JsonPropertyName("request_id")]
    public Guid RequestId { get; init; }

    [JsonPropertyName("highlight")]
    public HighlightPayload Highlight { get; init; } = new();
}

/// <summary>The highlight fields nested under <c>highlight</c> in a create-or-update request.</summary>
internal sealed record HighlightPayload
{
    [JsonPropertyName("bible_id")]
    public int BibleId { get; init; }

    [JsonPropertyName("passage_id")]
    public string PassageId { get; init; } = string.Empty;

    /// <summary>Hex color, without a leading '#' (e.g. "44aa44").</summary>
    [JsonPropertyName("color")]
    public string Color { get; init; } = string.Empty;
}
