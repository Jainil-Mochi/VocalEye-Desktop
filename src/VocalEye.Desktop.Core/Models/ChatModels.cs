namespace VocalEye.Desktop.Core.Models;

public record ModelSelectionDto(string Provider, string Model);

public record PendingApprovalDto(string ApprovalId, string ToolName, string ArgumentsJson);

public record ToolApprovalResponseDto(string ApprovalId, bool Approved);

public record ToolCallResultDto(string Name, string ArgumentsJson, bool Success, string Output);

public record ChatRequestDto(
    Guid? ConversationId,
    string? Message,
    string InputMode = "text",
    ModelSelectionDto? Model = null,
    List<ToolApprovalResponseDto>? Approvals = null);

public record ChatResponseDto(
    Guid ConversationId,
    string Reply,
    ModelSelectionDto ModelUsed,
    List<ToolCallResultDto> ToolCalls,
    List<PendingApprovalDto> PendingApprovals);

public record SseTextEventDto(string Text);

public record SseErrorEventDto(string Title, string Detail);
