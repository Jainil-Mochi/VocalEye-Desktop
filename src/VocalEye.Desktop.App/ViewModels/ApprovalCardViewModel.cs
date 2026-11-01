using CommunityToolkit.Mvvm.ComponentModel;
using VocalEye.Desktop.Core.Approvals;
using VocalEye.Desktop.Core.DTOs;

namespace VocalEye.Desktop.App.ViewModels;

/// <summary>One pending tool call, shown to the user with what it will really do.</summary>
public partial class ApprovalCardViewModel : ObservableObject
{
    public ApprovalCardViewModel(PendingApprovalDto dto, Guid conversationId, ToolRisk risk)
    {
        ApprovalId = dto.ApprovalId;
        ToolName = dto.ToolName;
        ConversationId = conversationId;
        Risk = risk == ToolRisk.Unknown ? ToolRiskExtensions.DefaultFor(dto.ToolName) : risk;
        RawArguments = ApprovalSummaryBuilder.PrettyJson(dto.ArgumentsJson);
        Summary = ApprovalSummaryBuilder.Build(dto.ToolName, dto.ArgumentsJson);
    }

    public string ApprovalId { get; }
    public string ToolName { get; }
    public Guid ConversationId { get; }
    public string DisplayName => ToolDisplay.Name(ToolName);
    public ToolRisk Risk { get; }
    public string RiskKey => Risk.ToString();
    public string RiskLabel => Risk.Label();
    public ApprovalSummary Summary { get; }
    public string RawArguments { get; }

    [ObservableProperty] private bool _showRawArguments;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsDecided))]
    private ApprovalDecision _decision;

    /// <summary>True once the decision was sent to the backend (or the turn was stopped).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsPending))]
    private bool _isResolved;

    public bool IsPending => !IsResolved;
    public bool IsDecided => Decision != ApprovalDecision.Undecided;

    public string StatusText => (IsResolved, Decision) switch
    {
        (true, ApprovalDecision.Approved) => "Approved",
        (true, ApprovalDecision.Denied) => "Denied",
        (true, _) => "Closed",
        (false, ApprovalDecision.Approved) => "Approved — waiting for your other choices",
        (false, ApprovalDecision.Denied) => "Denied — waiting for your other choices",
        _ => "Waiting for your decision"
    };
}
