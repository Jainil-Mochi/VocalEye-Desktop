namespace VocalEye.Desktop.Core.DTOs;

/// <summary>Optional per-turn model override: { provider, model }. The picker arrives in Phase 4.</summary>
public sealed class ModelSelectionDto
{
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
}
