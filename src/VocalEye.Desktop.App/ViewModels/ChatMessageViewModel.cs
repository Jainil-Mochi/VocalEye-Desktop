using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VocalEye.Desktop.App.ViewModels;

public partial class ChatMessageViewModel : ObservableObject
{
    public ChatMessageViewModel(string role, string text, IEnumerable<string>? toolNames = null)
    {
        Role = role;
        _text = text;
        ToolNames = toolNames?.ToList() ?? [];
        ToolResults.CollectionChanged += OnResultsChanged;
        Approvals.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasApprovals));
    }

    public string Role { get; }
    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => !IsUser;
    public IReadOnlyList<string> ToolNames { get; }
    public bool HasTools => ToolNames.Count > 0;

    /// <summary>"Used tools: …" is hidden once real tool results are shown for this message.</summary>
    public string ToolSummary => HasTools && ToolResults.Count == 0 ? "Used tools: " + string.Join(", ", ToolNames) : string.Empty;

    /// <summary>Tools that ran during this turn.</summary>
    public ObservableCollection<ToolResultViewModel> ToolResults { get; } = [];

    /// <summary>Tool calls waiting for the user (or already answered) from this turn.</summary>
    public ObservableCollection<ApprovalCardViewModel> Approvals { get; } = [];

    public bool HasApprovals => Approvals.Count > 0;
    public bool HasToolResults => ToolResults.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasText))]
    private string _text;

    [ObservableProperty] private bool _isStreaming;
    /// <summary>True when a reply was cut off (stopped or failed) and only partial text is shown.</summary>
    [ObservableProperty] private bool _isIncomplete;
    /// <summary>"provider/model" that produced the reply, when known.</summary>
    [ObservableProperty] private string? _modelLabel;

    public bool HasText => !string.IsNullOrWhiteSpace(Text);
    public bool HasContent => HasText || HasToolResults || HasApprovals;

    public void AppendText(string chunk) => Text += chunk;

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ToolSummary));
        OnPropertyChanged(nameof(HasToolResults));
    }
}
