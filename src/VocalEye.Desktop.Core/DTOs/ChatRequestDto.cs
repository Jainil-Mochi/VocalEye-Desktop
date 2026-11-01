namespace VocalEye.Desktop.Core.DTOs;

/// <summary>
/// Body of POST /api/chat/stream. Either <see cref="Message"/> or <see cref="Approvals"/> must be set.
/// Null properties are not serialized.
/// </summary>
public sealed class ChatRequestDto
{
    public Guid? ConversationId { get; init; }
    public string? Message { get; init; }
    public string InputMode { get; init; } = "text";
    public ModelSelectionDto? Model { get; init; }
    public List<ApprovalDecisionDto>? Approvals { get; init; }
}
