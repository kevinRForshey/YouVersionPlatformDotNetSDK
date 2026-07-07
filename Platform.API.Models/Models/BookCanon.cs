using System.Text.Json.Serialization;

using Platform.API.Models.Json;

namespace Platform.API.Models;

/// <summary>
/// The scriptural canon a Bible book belongs to.
/// </summary>
[JsonConverter(typeof(BookCanonJsonConverter))]
public enum BookCanon
{
    /// <summary>The canon could not be determined from the API response.</summary>
    Unknown,

    /// <summary>The Old Testament.</summary>
    OldTestament,

    /// <summary>The New Testament.</summary>
    NewTestament,

    /// <summary>The deuterocanonical (apocryphal) books.</summary>
    Deuterocanon
}
