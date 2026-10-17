using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace MonoGameEngine.Engine.Serialization;

/// <summary>Writes a <see cref="Vector2"/> as <c>{"x":1,"y":2}</c>.</summary>
public sealed class Vector2JsonConverter : JsonConverter<Vector2>
{
    /// <inheritdoc />
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected an object for Vector2.");
        float x = 0, y = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            if (string.Equals(name, "x", StringComparison.OrdinalIgnoreCase)) x = reader.GetSingle();
            else if (string.Equals(name, "y", StringComparison.OrdinalIgnoreCase)) y = reader.GetSingle();
            else reader.Skip();
        }
        return new Vector2(x, y);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteEndObject();
    }
}

/// <summary>Writes a <see cref="Color"/> as a <c>#RRGGBBAA</c> string.</summary>
public sealed class ColorJsonConverter : JsonConverter<Color>
{
    /// <inheritdoc />
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? throw new JsonException("Expected a colour string.");
        text = text.TrimStart('#');
        if (text.Length is not (6 or 8)) throw new JsonException($"Invalid colour '{text}'.");

        byte Part(int i) => byte.Parse(text.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Color(Part(0), Part(1), Part(2), text.Length == 8 ? Part(3) : (byte)255);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) =>
        writer.WriteStringValue($"#{value.R:X2}{value.G:X2}{value.B:X2}{value.A:X2}");
}
