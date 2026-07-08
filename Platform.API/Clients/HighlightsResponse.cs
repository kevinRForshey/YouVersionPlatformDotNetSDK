using System.Text.Json.Serialization;

using Platform.API.Models;

namespace Platform.API.Clients;

/// <summary>Response envelope for <c>GET /v1/highlights</c>.</summary>
internal sealed record HighlightsResponse
{
    [JsonPropertyName("data")]
    public List<Highlight> Data { get; init; } = [];
}

/// <summary>Response envelope for <c>GET /v1/highlights/recent-colors</c>.</summary>
internal sealed record RecentColorsResponse
{
    [JsonPropertyName("data")]
    public List<RecentColorEntry> Data { get; init; } = [];
}

/// <summary>A single entry in the recent-colors response.</summary>
internal sealed record RecentColorEntry
{
    [JsonPropertyName("color")]
    public string Color { get; init; } = string.Empty;
}
