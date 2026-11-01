namespace VocalEye.Desktop.Core.DTOs;

/// <summary>One answer to a pending tool approval. Only these two fields are ever sent.</summary>
public sealed class ApprovalDecisionDto
{
    public string ApprovalId { get; init; } = string.Empty;
    public bool Approved { get; init; }
}
