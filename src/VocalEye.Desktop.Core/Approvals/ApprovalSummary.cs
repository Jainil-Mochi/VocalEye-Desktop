namespace VocalEye.Desktop.Core.Approvals;

/// <summary>Plain-language description of what a pending tool call will do, built from its real arguments.</summary>
public sealed record ApprovalSummary(
    string Headline,
    IReadOnlyList<ApprovalField> Fields,
    IReadOnlyList<ApprovalPreview> Previews,
    bool ArgumentsReadable);
