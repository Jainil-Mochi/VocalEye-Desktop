namespace VocalEye.Desktop.Core.DTOs;

/// <summary>Item returned by GET /api/tools. Risk is ReadOnly, Write or Destructive.</summary>
public sealed class ToolDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Risk { get; init; }
}
