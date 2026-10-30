using CommunityToolkit.Mvvm.ComponentModel;
using VocalEye.Desktop.Core.DTOs;

namespace VocalEye.Desktop.App.ViewModels;

public partial class ConversationSummaryViewModel : ObservableObject
{
    public ConversationSummaryViewModel(ConversationSummaryDto dto)
    {
        Id = dto.ConversationId;
        _title = dto.Title;
        Model = dto.Model;
        UpdatedAt = dto.UpdatedAt;
        MessageCount = dto.MessageCount;
    }

    private ConversationSummaryViewModel(Guid id)
    {
        Id = id;
        IsDraft = true;
        UpdatedAt = DateTimeOffset.Now;
    }

    /// <summary>A client-side conversation that does not exist on the server until the first send.</summary>
    public static ConversationSummaryViewModel CreateDraft(Guid? id = null) => new(id ?? Guid.NewGuid());

    public Guid Id { get; }
    public string? Model { get; }
    public DateTimeOffset UpdatedAt { get; }
    public int MessageCount { get; }
    public bool IsDraft { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    private string? _title;

    /// <summary>Title derived locally (first user message) while the backend has not supplied one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    private string? _fallbackTitle;

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editTitle = string.Empty;

    public string DisplayTitle =>
        !string.IsNullOrWhiteSpace(Title) ? Title!
        : !string.IsNullOrWhiteSpace(FallbackTitle) ? FallbackTitle!
        : IsDraft ? "New chat"
        : $"Chat · {UpdatedAt.LocalDateTime:MMM d, h:mm tt}";
}
