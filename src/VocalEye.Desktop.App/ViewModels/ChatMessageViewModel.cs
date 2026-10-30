using CommunityToolkit.Mvvm.ComponentModel;

namespace VocalEye.Desktop.App.ViewModels;

public partial class ChatMessageViewModel : ObservableObject
{
    public ChatMessageViewModel(string role, string text, IEnumerable<string>? toolNames = null)
    {
        Role = role;
        _text = text;
        ToolNames = toolNames?.ToList() ?? [];
    }

    public string Role { get; }
    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => !IsUser;
    public IReadOnlyList<string> ToolNames { get; }
    public bool HasTools => ToolNames.Count > 0;
    public string ToolSummary => HasTools ? "Used tools: " + string.Join(", ", ToolNames) : string.Empty;

    [ObservableProperty] private string _text;
    [ObservableProperty] private bool _isStreaming;
    /// <summary>True when a reply was cut off (stopped or failed) and only partial text is shown.</summary>
    [ObservableProperty] private bool _isIncomplete;

    public void AppendText(string chunk) => Text += chunk;
}
