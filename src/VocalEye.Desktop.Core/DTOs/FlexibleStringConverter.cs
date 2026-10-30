using System.Text.Json;
using System.Text.Json.Serialization;

namespace VocalEye.Desktop.Core.DTOs;

/// <summary>
/// Reads a JSON string, or an object such as { "provider": "x", "model": "y" }, into a display string.
/// </summary>
public sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        switch (root.ValueKind)
        {
            case JsonValueKind.String:
                return root.GetString();

            case JsonValueKind.Object:
                var provider = GetString(root, "provider");
                var model = GetString(root, "model") ?? GetString(root, "name") ?? GetString(root, "id");
                if (provider is not null && model is not null) return $"{provider}/{model}";
                return model ?? provider ?? root.GetRawText();

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;

            default:
                return root.GetRawText();
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
