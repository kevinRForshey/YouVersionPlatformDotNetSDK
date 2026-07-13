using System.Collections.Generic;

namespace Platform.API.OAuth;

/// <summary>
/// The outcome of a Data Exchange approval, as reported by the <c>data_exchange_status</c> query
/// parameter on the callback YouVersion redirects to.
/// </summary>
public enum DataExchangeStatus
{
    /// <summary>
    /// No recognized <c>data_exchange_status</c> value was present on the callback.
    /// </summary>
    Unknown,

    /// <summary>The user granted the requested permission(s).</summary>
    Granted,

    /// <summary>The user cancelled or denied the approval page.</summary>
    Cancelled,

    /// <summary>The approval failed; see <see cref="DataExchangeCallbackResult.Error"/> and <see cref="DataExchangeCallbackResult.ErrorDescription"/>.</summary>
    Error
}

/// <summary>
/// The parsed outcome of a Data Exchange approval callback. Returned by
/// <see cref="IYouVersionOAuthClient.ParseDataExchangeCallback"/> and
/// <see cref="IYouVersionOAuthClient.CompleteDataExchangeApprovalAsync"/>.
/// </summary>
public sealed record DataExchangeCallbackResult
{
    /// <summary>The overall outcome of the approval.</summary>
    public DataExchangeStatus Status { get; init; }

    /// <summary>The permissions the user granted, if any.</summary>
    public IReadOnlyList<string> GrantedPermissions { get; init; } = [];

    /// <summary>The permissions the user denied, if any.</summary>
    public IReadOnlyList<string> DeniedPermissions { get; init; } = [];

    /// <summary>The <c>error</c> query parameter, if the approval failed.</summary>
    public string? Error { get; init; }

    /// <summary>The <c>error_description</c> query parameter, if the approval failed.</summary>
    public string? ErrorDescription { get; init; }
}
