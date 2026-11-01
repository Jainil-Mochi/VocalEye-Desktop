namespace VocalEye.Desktop.Core.DTOs;

/// <summary>
/// Item returned by GET /api/conversations/{id}/messages. Tool messages also carry <see cref="ToolResults"/>
/// (backend slice B6), so old tool cards can be redrawn after a restart.
/// </summary>
public sealed class ConversationMessageDto
{
    private List<ToolCallDto> _toolResults = [];
    private List<string> _toolNames = [];

    public int Sequence { get; init; }
    public string Role { get; init; } = "assistant";
    public string Text { get; init; } = string.Empty;

    public List<string> ToolNames
    {
        get => _toolNames;
        init => _toolNames = value ?? [];
    }

    public List<ToolCallDto> ToolResults
    {
        get => _toolResults;
        init => _toolResults = value ?? [];
    }
}
