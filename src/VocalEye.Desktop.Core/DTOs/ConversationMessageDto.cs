namespace VocalEye.Desktop.Core.DTOs;

/// <summary>Item returned by GET /api/conversations/{id}/messages.</summary>
public sealed class ConversationMessageDto
{
    public int Sequence { get; init; }
    public string Role { get; init; } = "assistant";
    public string Text { get; init; } = string.Empty;
    public List<string> ToolNames { get; init; } = [];
}
