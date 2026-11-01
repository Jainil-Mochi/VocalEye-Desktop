using System.Text.Json.Serialization;

namespace VocalEye.Desktop.Core.DTOs;

/// <summary>
/// A tool that actually ran: an item of <c>toolCalls</c> in a chat response, or of <c>toolResults</c> in
/// conversation history. A tool failure is not an HTTP error: Success is false and Output holds the reason.
/// </summary>
public sealed class ToolCallDto
{
    private List<ToolArtifactDto> _artifacts = [];

    public string Name { get; init; } = string.Empty;

    /// <summary>History may name the tool "toolName" instead of "name"; see <see cref="ResolvedName"/>.</summary>
    public string? ToolName { get; init; }

    public string? ArgumentsJson { get; init; }
    public bool Success { get; init; }
    public string? Output { get; init; }

    public List<ToolArtifactDto> Artifacts
    {
        get => _artifacts;
        init => _artifacts = value ?? [];
    }

    [JsonIgnore]
    public string ResolvedName => !string.IsNullOrWhiteSpace(Name) ? Name : ToolName ?? string.Empty;
}
