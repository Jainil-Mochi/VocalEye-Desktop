using System.Text.Json;
using System.Text.Json.Serialization;

namespace VocalEye.Desktop.Core.DTOs;

/// <summary>
/// Structured tool result (backend slice B6). <see cref="Kind"/> is "file", "folder" or "url";
/// <see cref="Value"/> is the path or the link.
/// </summary>
public sealed class ToolArtifactDto
{
    private static readonly string[] LegacyNames = ["path", "filePath", "fullPath", "location", "url", "uri"];

    public string Kind { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    /// <summary>Unmapped properties, kept so an older or renamed field (for example "path") still works.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>"value" if present, otherwise the first legacy field that holds text.</summary>
    [JsonIgnore]
    public string ResolvedValue
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Value)) return Normalize(Value);
            if (Extra is null) return string.Empty;

            foreach (var name in LegacyNames)
                foreach (var pair in Extra)
                    if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)
                        && pair.Value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(pair.Value.GetString()))
                        return Normalize(pair.Value.GetString()!);

            return string.Empty;
        }
    }

    private static string Normalize(string value) =>
        value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri.LocalPath
            : value;
}
