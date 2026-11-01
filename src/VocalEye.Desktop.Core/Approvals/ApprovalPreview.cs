namespace VocalEye.Desktop.Core.Approvals;

/// <summary>A block of text the user can inspect before approving (file content, email body, a project file).</summary>
public sealed record ApprovalPreview(string Label, string Text, string? Caption, bool ExpandedByDefault);
