using System.Text.Json;

namespace VocalEye.Desktop.Core.DTOs;

public sealed class ModelUsedDto
{
    public string? Provider { get; init; }
    public string? Model { get; init; }
}

/// <summary>Payload of the SSE "completed" event.</summary>
public sealed class ChatCompletedDto
{
    public Guid ConversationId { get; init; }
    public string Reply { get; init; } = string.Empty;
    public ModelUsedDto? ModelUsed { get; init; }

    // Kept as raw JSON: tool calls / approvals are Phase 3 and their shape is not needed yet.
    public List<JsonElement> ToolCalls { get; init; } = [];
    public List<JsonElement> PendingApprovals { get; init; } = [];
}
