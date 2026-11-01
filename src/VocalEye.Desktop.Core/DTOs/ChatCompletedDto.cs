namespace VocalEye.Desktop.Core.DTOs;

/// <summary>Payload of the SSE "completed" event (and the body of POST /api/chat).</summary>
public sealed class ChatCompletedDto
{
    private List<ToolCallDto> _toolCalls = [];
    private List<PendingApprovalDto> _pendingApprovals = [];

    public Guid ConversationId { get; init; }
    public string Reply { get; init; } = string.Empty;
    public ModelUsedDto? ModelUsed { get; init; }

    public List<ToolCallDto> ToolCalls
    {
        get => _toolCalls;
        init => _toolCalls = value ?? [];
    }

    public List<PendingApprovalDto> PendingApprovals
    {
        get => _pendingApprovals;
        init => _pendingApprovals = value ?? [];
    }
}
