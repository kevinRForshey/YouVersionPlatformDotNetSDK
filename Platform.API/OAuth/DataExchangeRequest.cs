using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Platform.API.OAuth;

/// <summary>Request body sent to <c>POST /data-exchange/token</c>.</summary>
internal sealed record DataExchangeRequest
{
    [JsonPropertyName("requested_permissions")]
    public IReadOnlyList<string> RequestedPermissions { get; init; } = [];
}
