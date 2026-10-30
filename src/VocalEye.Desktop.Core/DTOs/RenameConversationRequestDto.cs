namespace VocalEye.Desktop.Core.DTOs;

/// <summary>Body of PATCH /api/conversations/{id} (requires backend slice B4).</summary>
public sealed class RenameConversationRequestDto
{
    public string Title { get; init; } = string.Empty;
}
