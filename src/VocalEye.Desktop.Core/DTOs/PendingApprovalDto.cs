namespace VocalEye.Desktop.Core.DTOs;

/// <summary>A tool call that is waiting for the user. Nothing in this list has run.</summary>
public sealed class PendingApprovalDto
{
    public string ApprovalId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public string ArgumentsJson { get; init; } = "{}";
}
