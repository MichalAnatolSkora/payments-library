using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Payment.Core.P24.Models;

/// <summary>
/// Reads an optional integer that P24 may send as a number or as a numeric string. Anything else
/// (an empty string, a decimal, a boolean, an object) becomes null instead of failing the whole response.
/// </summary>
internal sealed class LenientIntegerConverter<T> : JsonConverter<T?> where T : struct, IBinaryInteger<T>
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                ReadOnlySpan<byte> utf8 = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
                return T.TryParse(utf8, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
            case JsonTokenType.String:
                return T.TryParse(reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                reader.Skip();
                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteRawValue(value.Value.ToString(null, CultureInfo.InvariantCulture));
        }
    }
}
