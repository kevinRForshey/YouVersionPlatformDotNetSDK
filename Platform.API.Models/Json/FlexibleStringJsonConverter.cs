using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.API.Models.Json;

/// <summary>
/// Reads a JSON string or number as a <see cref="string"/>. The Platform API's
/// <c>/index</c> endpoint sends chapter/verse <c>title</c> fields as bare numbers for regular
/// content but as strings for special sections (e.g. an introduction titled <c>"Intro"</c>),
/// so a single model shape must tolerate either wire representation.
/// </summary>
internal sealed class FlexibleStringJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => string.Empty,
            _ => throw new JsonException($"Cannot convert token type {reader.TokenType} to a string.")
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
