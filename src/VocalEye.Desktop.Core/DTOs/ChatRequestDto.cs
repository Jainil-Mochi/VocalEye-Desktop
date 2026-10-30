namespace VocalEye.Desktop.Core.DTOs;

/// <summary>Body of POST /api/chat/stream (Phase 2 minimum).</summary>
public sealed class ChatRequestDto
{
    public Guid? ConversationId { get; init; }
    public string? Message { get; init; }
    public string InputMode { get; init; } = "text";
}
