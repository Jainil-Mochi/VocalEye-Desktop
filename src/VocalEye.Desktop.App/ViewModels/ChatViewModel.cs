using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VocalEye.Desktop.App.Services;
using VocalEye.Desktop.Core.Approvals;
using VocalEye.Desktop.Core.DTOs;
using VocalEye.Desktop.Core.Exceptions;
using VocalEye.Desktop.Core.Interfaces;
using VocalEye.Desktop.Core.Streaming;

namespace VocalEye.Desktop.App.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly IChatService _chat;
    private readonly IDialogService _dialogs;
    private readonly IToolCatalogService? _tools;
    private readonly IShellLauncher? _launcher;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly Dictionary<string, ToolRisk> _toolRisks = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _sendCts;
    private CancellationTokenSource? _historyCts;
    private int _historyGeneration;
    private bool _suppressSelectionLoad;
    private ChatRequestDto? _lastRequest;
    private IReadOnlyList<ApprovalCardViewModel>? _lastApprovalCards;

    public ChatViewModel(IChatService chat, IDialogService dialogs,
        IToolCatalogService? tools = null, IShellLauncher? launcher = null)
    {
        _chat = chat;
        _dialogs = dialogs;
        _tools = tools;
        _launcher = launcher;
        Messages.CollectionChanged += OnMessagesChanged;
        Conversations.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasConversations));
        PendingApprovals.CollectionChanged += OnPendingApprovalsChanged;
    }

    public ObservableCollection<ConversationSummaryViewModel> Conversations { get; } = [];
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    /// <summary>Every tool call currently waiting for the user's decision (cards live inside their messages).</summary>
    public ObservableCollection<ApprovalCardViewModel> PendingApprovals { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string _draftMessage = string.Empty;

    [ObservableProperty] private ConversationSummaryViewModel? _selectedConversation;

    /// <summary>True while the conversation list or a conversation's history is loading.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopResponseCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApproveCommand))]
    [NotifyCanExecuteChangedFor(nameof(DenyCommand))]
    [NotifyCanExecuteChangedFor(nameof(DenyAllCommand))]
    private bool _isStreaming;

    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    private bool _canRetry;

    /// <summary>Text for the sidebar "Backend status" area; set by the Phase 1 connection monitor.</summary>
    [ObservableProperty] private string _connectionStatus = "Checking backend…";
    [ObservableProperty] private bool _isConnected;

    public bool HasConversations => Conversations.Count > 0;
    public bool ShowWelcome => Messages.Count == 0 && !IsBusy;
    public bool HasPendingApprovals => PendingApprovals.Count > 0;

    /// <summary>"2 tool actions are waiting for your decision (1 still to decide)".</summary>
    public string PendingApprovalsText
    {
        get
        {
            var total = PendingApprovals.Count;
            var undecided = PendingApprovals.Count(c => !c.IsDecided);
            var noun = total == 1 ? "tool action is" : "tool actions are";
            return total == 0 ? string.Empty
                : $"{total} {noun} waiting for your decision" + (undecided < total ? $" — {undecided} left to decide" : string.Empty);
        }
    }

    /// <summary>Raised when the view should focus the composer.</summary>
    public event EventHandler? FocusComposerRequested;
    /// <summary>Raised when new content was added and the view may scroll to the end.</summary>
    public event EventHandler? ScrollToEndRequested;

    // ───────────────────────── Initialization / list ─────────────────────────

    public async Task InitializeAsync()
    {
        await LoadConversationsAsync(silent: false);
        await LoadToolCatalogAsync();
    }

    [RelayCommand]
    private Task RefreshConversationsAsync() => LoadConversationsAsync(silent: false);

    private async Task LoadToolCatalogAsync()
    {
        if (_tools is null) return;
        try
        {
            foreach (var tool in await _tools.GetToolsAsync(CancellationToken.None))
                _toolRisks[tool.Name] = ToolRiskExtensions.Parse(tool.Risk);
        }
        catch (ChatApiException) { /* risk badges fall back to built-in defaults */ }
    }

    private async Task LoadConversationsAsync(bool silent)
    {
        if (!silent) IsBusy = true;
        try
        {
            var list = await _chat.GetConversationsAsync(50, CancellationToken.None);

            var selectedId = SelectedConversation?.Id;
            var fallbacks = Conversations.Where(c => c.FallbackTitle is not null)
                                         .ToDictionary(c => c.Id, c => c.FallbackTitle);
            var drafts = Conversations.Where(c => c.IsDraft && list.All(l => l.ConversationId != c.Id)).ToList();

            _suppressSelectionLoad = true;
            try
            {
                Conversations.Clear();
                foreach (var draft in drafts) Conversations.Add(draft);
                foreach (var dto in list.OrderByDescending(x => x.UpdatedAt))
                {
                    var vm = new ConversationSummaryViewModel(dto);
                    if (fallbacks.TryGetValue(vm.Id, out var fb)) vm.FallbackTitle = fb;
                    Conversations.Add(vm);
                }
                SelectedConversation = selectedId is { } id ? Conversations.FirstOrDefault(c => c.Id == id) : null;
            }
            finally { _suppressSelectionLoad = false; }

            if (selectedId is not null && SelectedConversation is null)
            {
                Messages.Clear(); // the selected conversation no longer exists on the server
                PendingApprovals.Clear();
            }
        }
        catch (ChatApiException ex)
        {
            if (!silent || ErrorMessage is null)
            {
                ErrorMessage = ChatErrorFormatter.Format(ex, "load conversations");
                CanRetry = false;
            }
        }
        finally
        {
            if (!silent) IsBusy = false;
        }
    }

    // ───────────────────────── New chat / selection ─────────────────────────

    [RelayCommand]
    private void NewChat()
    {
        CancelActiveOperations();
        ClearThread();
        ErrorMessage = null;
        CanRetry = false;
        DraftMessage = string.Empty;
        CreateAndSelectDraft();
        FocusComposerRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearThread()
    {
        Messages.Clear();
        PendingApprovals.Clear();
        _lastRequest = null;
        _lastApprovalCards = null;
    }

    private ConversationSummaryViewModel CreateAndSelectDraft()
    {
        var draft = ConversationSummaryViewModel.CreateDraft();
        Conversations.Insert(0, draft);
        _suppressSelectionLoad = true;
        try { SelectedConversation = draft; }
        finally { _suppressSelectionLoad = false; }
        return draft;
    }

    partial void OnSelectedConversationChanged(ConversationSummaryViewModel? value)
    {
        if (_suppressSelectionLoad) return;
        _ = LoadHistoryAsync(value);
    }

    private async Task LoadHistoryAsync(ConversationSummaryViewModel? conversation)
    {
        // Switching chats cancels an in-progress reply (documented UX policy) and any older history load.
        CancelActiveOperations();
        var generation = ++_historyGeneration;
        ClearThread();
        ErrorMessage = null;
        CanRetry = false;

        if (conversation is null || conversation.IsDraft)
        {
            IsBusy = false;
            return;
        }

        var cts = new CancellationTokenSource();
        _historyCts = cts;
        IsBusy = true;
        try
        {
            var history = await _chat.GetMessagesAsync(conversation.Id, cts.Token);
            if (generation != _historyGeneration) return; // a newer selection superseded this request

            for (var i = 0; i < history.Count; i++)
            {
                var m = history[i];
                var blank = string.IsNullOrWhiteSpace(m.Text);
                if (blank && m.ToolResults.Count == 0 && m.ToolNames.Count == 0) continue;

                // An assistant row that only says "Used tools: x" is redundant when the next row carries the real results.
                if (blank && m.ToolResults.Count == 0 && i + 1 < history.Count && history[i + 1].ToolResults.Count > 0) continue;

                var vm = new ChatMessageViewModel(m.Role, m.Text, m.ToolNames);
                foreach (var result in m.ToolResults) vm.ToolResults.Add(CreateToolResult(result));
                Messages.Add(vm);
            }

            // Resumable approvals (backend slice B6): tool calls still waiting for the user.
            IReadOnlyList<PendingApprovalDto>? pending = null;
            try { pending = await _chat.GetPendingApprovalsAsync(conversation.Id, cts.Token); }
            catch (ChatApiException) { /* older backend, or nothing to resume */ }
            if (generation != _historyGeneration) return;

            if (pending is { Count: > 0 })
            {
                var host = Messages.Count > 0 && Messages[^1].IsAssistant ? Messages[^1] : null;
                if (host is null)
                {
                    host = new ChatMessageViewModel("assistant", string.Empty);
                    Messages.Add(host);
                }
                foreach (var p in pending) AddApprovalCard(host, p, conversation.Id);
            }
            ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) { /* superseded */ }
        catch (ChatApiException ex) when (generation == _historyGeneration)
        {
            ErrorMessage = ChatErrorFormatter.Format(ex, "load this conversation");
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                await LoadConversationsAsync(silent: true);
        }
        finally
        {
            if (generation == _historyGeneration) IsBusy = false;
            if (ReferenceEquals(_historyCts, cts)) _historyCts = null;
            cts.Dispose();
        }
    }

    private void CancelActiveOperations()
    {
        _sendCts?.Cancel();
        _historyCts?.Cancel();
    }

    // ───────────────────────── Sending / streaming ─────────────────────────

    private bool CanSend() => !IsStreaming && !HasPendingApprovals && !string.IsNullOrWhiteSpace(DraftMessage);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendMessageAsync()
    {
        var text = DraftMessage?.Trim();
        if (IsStreaming || HasPendingApprovals || string.IsNullOrWhiteSpace(text)) return;

        var conversation = SelectedConversation ?? CreateAndSelectDraft();
        conversation.FallbackTitle ??= MakeTitle(text);

        DraftMessage = string.Empty;
        Messages.Add(new ChatMessageViewModel("user", text));
        await RunTurnAsync(new ChatRequestDto { ConversationId = conversation.Id, Message = text });
    }

    private bool CanRetryTurn() => CanRetry && !IsStreaming && _lastRequest is not null;

    [RelayCommand(CanExecute = nameof(CanRetryTurn))]
    private async Task RetryAsync()
    {
        if (_lastRequest is null) return;
        // The user's message (or the approval decisions) are already shown; nothing is added twice.
        await RunTurnAsync(_lastRequest, _lastApprovalCards);
    }

    /// <param name="approvalCards">Set when this request answers pending approvals.</param>
    private async Task RunTurnAsync(ChatRequestDto request, IReadOnlyList<ApprovalCardViewModel>? approvalCards = null)
    {
        ErrorMessage = null;
        CanRetry = false;
        _lastRequest = request;
        _lastApprovalCards = approvalCards;

        var conversationId = request.ConversationId ?? Guid.Empty;
        var assistant = new ChatMessageViewModel("assistant", string.Empty) { IsStreaming = true };
        Messages.Add(assistant);
        IsStreaming = true;

        var cts = new CancellationTokenSource();
        _sendCts = cts;
        var batcher = new StreamingTextBatcher(chunk => OnUi(() =>
        {
            assistant.AppendText(chunk);
            ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
        }));

        ChatCompletedDto? completed = null;
        var cancelled = false;
        try
        {
            completed = await _chat.StreamMessageAsync(request, batcher.Append, cts.Token);
        }
        catch (OperationCanceledException) { cancelled = true; /* stopped by the user or by navigation */ }
        catch (ChatApiException ex)
        {
            ErrorMessage = ChatErrorFormatter.Format(ex);
            CanRetry = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Something went wrong: " + ex.Message;
            CanRetry = true;
        }
        finally
        {
            await batcher.DisposeAsync(); // flushes any buffered text
            assistant.IsStreaming = false;
            if (ReferenceEquals(_sendCts, cts)) _sendCts = null;
            cts.Dispose();
        }

        // Once the backend has seen the decisions (answered, or interrupted mid-run) the cards are closed.
        // After a plain failure they stay open so Retry can send the same decisions again.
        if (approvalCards is not null && (completed is not null || cancelled))
        {
            foreach (var card in approvalCards)
            {
                card.IsResolved = true;
                PendingApprovals.Remove(card);
            }
            if (cancelled)
            {
                ErrorMessage = "Stopped. Tools you approved may already have run — check the results before asking again.";
                CanRetry = false;
            }
        }

        if (completed is not null)
            FinalizeCompleted(assistant, completed, conversationId);
        else if (!assistant.HasContent)
            Messages.Remove(assistant);
        else if (assistant.HasText)
            assistant.IsIncomplete = true; // keep the partial reply, flagged

        IsStreaming = false;
        OnPropertyChanged(nameof(PendingApprovalsText));
        await LoadConversationsAsync(silent: true);
    }

    private void FinalizeCompleted(ChatMessageViewModel assistant, ChatCompletedDto completed, Guid requestConversationId)
    {
        var conversationId = completed.ConversationId != Guid.Empty ? completed.ConversationId : requestConversationId;

        if (!string.IsNullOrEmpty(completed.Reply))
            assistant.Text = completed.Reply; // authoritative final text

        if (completed.ModelUsed is { Model: { Length: > 0 } model })
            assistant.ModelLabel = completed.ModelUsed.Provider is { Length: > 0 } p ? $"{p}/{model}" : model;

        foreach (var call in completed.ToolCalls)
            assistant.ToolResults.Add(CreateToolResult(call));

        foreach (var pending in completed.PendingApprovals)
            AddApprovalCard(assistant, pending, conversationId);

        // A turn that only asks for approval, or only ran tools, legitimately has no text.
        if (!assistant.HasContent)
        {
            Messages.Remove(assistant);
            var who = assistant.ModelLabel is { Length: > 0 } m ? $" ({m})" : string.Empty;
            ErrorMessage = $"The model{who} returned nothing for this request. This can happen when a tool call is too large to finish, such as a whole website. Try Retry, a smaller request, or a different model.";
            CanRetry = true; // nothing ran, so resending cannot repeat an action
        }

        ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
    }

    private ToolResultViewModel CreateToolResult(ToolCallDto call) =>
        new(call, _launcher, ReportArtifactError);

    private void ReportArtifactError(string message)
    {
        ErrorMessage = message;
        CanRetry = false;
    }

    private void AddApprovalCard(ChatMessageViewModel host, PendingApprovalDto pending, Guid conversationId)
    {
        var risk = _toolRisks.TryGetValue(pending.ToolName, out var known) ? known : ToolRisk.Unknown;
        var card = new ApprovalCardViewModel(pending, conversationId, risk);
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ApprovalCardViewModel.Decision))
                OnPropertyChanged(nameof(PendingApprovalsText));
        };
        host.Approvals.Add(card);
        PendingApprovals.Add(card);
    }

    [RelayCommand(CanExecute = nameof(IsStreaming))]
    private void StopResponse() => _sendCts?.Cancel();

    [RelayCommand]
    private void DismissError()
    {
        ErrorMessage = null;
        CanRetry = false;
    }

    // ───────────────────────── Approvals ─────────────────────────

    private bool CanDecide(ApprovalCardViewModel? card) => !IsStreaming && card is { IsResolved: false };

    [RelayCommand(CanExecute = nameof(CanDecide))]
    private Task ApproveAsync(ApprovalCardViewModel? card) => DecideAsync(card, ApprovalDecision.Approved);

    [RelayCommand(CanExecute = nameof(CanDecide))]
    private Task DenyAsync(ApprovalCardViewModel? card) => DecideAsync(card, ApprovalDecision.Denied);

    private bool CanDenyAll() => !IsStreaming && HasPendingApprovals;

    /// <summary>Safe shortcut: deny everything that is waiting. There is deliberately no "approve all".</summary>
    [RelayCommand(CanExecute = nameof(CanDenyAll))]
    private async Task DenyAllAsync()
    {
        foreach (var card in PendingApprovals) card.Decision = ApprovalDecision.Denied;
        await SubmitApprovalsAsync();
    }

    private async Task DecideAsync(ApprovalCardViewModel? card, ApprovalDecision decision)
    {
        if (card is null || card.IsResolved || IsStreaming) return;
        card.Decision = decision;

        // The backend wants every pending call answered together, so wait until the last one is decided.
        if (PendingApprovals.Any(c => !c.IsDecided)) return;
        await SubmitApprovalsAsync();
    }

    private async Task SubmitApprovalsAsync()
    {
        var cards = PendingApprovals.ToList();
        if (cards.Count == 0 || cards.Any(c => !c.IsDecided) || IsStreaming) return;

        var request = new ChatRequestDto
        {
            ConversationId = cards[0].ConversationId,
            // Only approvalId + approved are sent; the arguments the user saw are never re-sent or edited.
            Approvals = cards.Select(c => new ApprovalDecisionDto
            {
                ApprovalId = c.ApprovalId,
                Approved = c.Decision == ApprovalDecision.Approved
            }).ToList()
        };
        await RunTurnAsync(request, cards);
    }

    private void OnPendingApprovalsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasPendingApprovals));
        OnPropertyChanged(nameof(PendingApprovalsText));
        SendMessageCommand.NotifyCanExecuteChanged();
        DenyAllCommand.NotifyCanExecuteChanged();
    }

    // ───────────────────────── Rename / delete ─────────────────────────

    [RelayCommand]
    private void BeginRename(ConversationSummaryViewModel? conversation)
    {
        conversation ??= SelectedConversation;
        if (conversation is null || conversation.IsDraft) return; // drafts aren't on the server yet
        conversation.EditTitle = conversation.DisplayTitle;
        conversation.IsEditing = true;
    }

    [RelayCommand]
    private void CancelRename(ConversationSummaryViewModel? conversation)
    {
        if (conversation is not null) conversation.IsEditing = false;
    }

    [RelayCommand]
    private async Task RenameConversationAsync(ConversationSummaryViewModel? conversation)
    {
        conversation ??= SelectedConversation;
        if (conversation is null || conversation.IsDraft) return;

        var title = conversation.EditTitle?.Trim();
        if (string.IsNullOrEmpty(title) || title == conversation.DisplayTitle)
        {
            conversation.IsEditing = false;
            return;
        }

        try
        {
            await _chat.RenameConversationAsync(conversation.Id, title, CancellationToken.None);
            conversation.Title = title;       // only after the server confirmed
            conversation.IsEditing = false;
            ErrorMessage = null;
        }
        catch (ChatApiException ex)
        {
            // Leave the editor open so the user's text isn't lost.
            ErrorMessage = ChatErrorFormatter.Format(ex, "rename the conversation");
            CanRetry = false;
        }
    }

    [RelayCommand]
    private async Task DeleteConversationAsync(ConversationSummaryViewModel? conversation)
    {
        conversation ??= SelectedConversation;
        if (conversation is null) return;

        var confirmed = await _dialogs.ConfirmAsync("Delete conversation",
            $"Delete \"{conversation.DisplayTitle}\"? This can't be undone.");
        if (!confirmed) return;

        if (!conversation.IsDraft)
        {
            try { await _chat.DeleteConversationAsync(conversation.Id, CancellationToken.None); }
            catch (ChatApiException ex)
            {
                ErrorMessage = ChatErrorFormatter.Format(ex, "delete the conversation");
                CanRetry = false;
                return; // nothing was removed locally
            }
        }

        var wasSelected = SelectedConversation?.Id == conversation.Id;
        var index = Conversations.IndexOf(conversation);
        if (wasSelected) CancelActiveOperations();
        Conversations.Remove(conversation);

        if (wasSelected)
        {
            ClearThread();
            ErrorMessage = null;
            SelectedConversation = Conversations.Count == 0
                ? null
                : Conversations[Math.Min(Math.Max(index, 0), Conversations.Count - 1)];
            // OnSelectedConversationChanged loads the next conversation's history (or clears the view).
            if (SelectedConversation is null) FocusComposerRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    // ───────────────────────── Helpers ─────────────────────────

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ShowWelcome));
        if (e.Action == NotifyCollectionChangedAction.Add)
            ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Runs on the UI thread. Streaming chunks arrive on a pool thread via the batcher.</summary>
    private void OnUi(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui) action();
        else _ui.Send(_ => action(), null);
    }

    private static string MakeTitle(string text)
    {
        var singleLine = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= 40 ? singleLine : singleLine[..40].TrimEnd() + "…";
    }
}
