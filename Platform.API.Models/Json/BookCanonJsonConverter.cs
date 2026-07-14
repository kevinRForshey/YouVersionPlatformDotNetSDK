using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.API.Models.Json;

/// <summary>
/// Maps the wire's snake_case <c>canon</c> values to <see cref="BookCanon"/>. Unrecognized
/// values deserialize to <see cref="BookCanon.Unknown"/> instead of throwing, so a future
/// canon addition on the API side doesn't break deserialization of the rest of the index.
/// </summary>
internal sealed class BookCanonJsonConverter : JsonConverter<BookCanon>
{
    public override BookCanon Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() switch
        {
            "old_testament" => BookCanon.OldTestament,
            "new_testament" => BookCanon.NewTestament,
            "deuterocanon" => BookCanon.Deuterocanon,
            _ => BookCanon.Unknown
        };

    public override void Write(Utf8JsonWriter writer, BookCanon value, JsonSerializerOptions options)
        => writer.WriteStringValue(value switch
        {
            BookCanon.OldTestament => "old_testament",
            BookCanon.NewTestament => "new_testament",
            BookCanon.Deuterocanon => "deuterocanon",
            _ => "unknown"
        });
}
