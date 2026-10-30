using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VocalEye.Desktop.App.Services;
using VocalEye.Desktop.Core.DTOs;
using VocalEye.Desktop.Core.Exceptions;
using VocalEye.Desktop.Core.Interfaces;
using VocalEye.Desktop.Core.Streaming;

namespace VocalEye.Desktop.App.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly IChatService _chat;
    private readonly IDialogService _dialogs;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;

    private CancellationTokenSource? _sendCts;
    private CancellationTokenSource? _historyCts;
    private int _historyGeneration;
    private bool _suppressSelectionLoad;
    private Guid _lastConversationId;
    private string? _lastMessage;

    public ChatViewModel(IChatService chat, IDialogService dialogs)
    {
        _chat = chat;
        _dialogs = dialogs;
        Messages.CollectionChanged += OnMessagesChanged;
        Conversations.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasConversations));
    }

    public ObservableCollection<ConversationSummaryViewModel> Conversations { get; } = [];
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

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

    /// <summary>Raised when the view should focus the composer.</summary>
    public event EventHandler? FocusComposerRequested;
    /// <summary>Raised when new content was added and the view may scroll to the end.</summary>
    public event EventHandler? ScrollToEndRequested;

    // ───────────────────────── Initialization / list ─────────────────────────

    public Task InitializeAsync() => LoadConversationsAsync(silent: false);

    [RelayCommand]
    private Task RefreshConversationsAsync() => LoadConversationsAsync(silent: false);

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
                Messages.Clear(); // the selected conversation no longer exists on the server
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
        Messages.Clear();
        ErrorMessage = null;
        CanRetry = false;
        DraftMessage = string.Empty;
        CreateAndSelectDraft();
        FocusComposerRequested?.Invoke(this, EventArgs.Empty);
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
        Messages.Clear();
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

            foreach (var m in history)
            {
                if (string.IsNullOrWhiteSpace(m.Text) && m.ToolNames.Count == 0) continue;
                Messages.Add(new ChatMessageViewModel(m.Role, m.Text, m.ToolNames));
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

    private bool CanSend() => !IsStreaming && !string.IsNullOrWhiteSpace(DraftMessage);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendMessageAsync()
    {
        var text = DraftMessage?.Trim();
        if (IsStreaming || string.IsNullOrWhiteSpace(text)) return;

        var conversation = SelectedConversation ?? CreateAndSelectDraft();
        conversation.FallbackTitle ??= MakeTitle(text);

        DraftMessage = string.Empty;
        Messages.Add(new ChatMessageViewModel("user", text));
        await RunTurnAsync(conversation.Id, text);
    }

    private bool CanRetryTurn() => CanRetry && !IsStreaming && _lastMessage is not null;

    [RelayCommand(CanExecute = nameof(CanRetryTurn))]
    private async Task RetryAsync()
    {
        if (_lastMessage is null) return;
        // The user's message is already shown; do not add it again.
        await RunTurnAsync(_lastConversationId, _lastMessage);
    }

    private async Task RunTurnAsync(Guid conversationId, string text)
    {
        ErrorMessage = null;
        CanRetry = false;
        _lastConversationId = conversationId;
        _lastMessage = text;

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
        try
        {
            var request = new ChatRequestDto { ConversationId = conversationId, Message = text };
            completed = await _chat.StreamMessageAsync(request, batcher.Append, cts.Token);
        }
        catch (OperationCanceledException) { /* stopped by the user or by navigation */ }
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

        if (completed is not null)
            FinalizeCompleted(assistant, completed);
        else if (string.IsNullOrEmpty(assistant.Text))
            Messages.Remove(assistant);
        else
            assistant.IsIncomplete = true; // keep the partial reply, flagged

        IsStreaming = false;
        await LoadConversationsAsync(silent: true);
    }

    private void FinalizeCompleted(ChatMessageViewModel assistant, ChatCompletedDto completed)
    {
        if (!string.IsNullOrEmpty(completed.Reply))
        {
            assistant.Text = completed.Reply; // authoritative final text
        }

        if (string.IsNullOrWhiteSpace(assistant.Text))
        {
            Messages.Remove(assistant);
            ErrorMessage = completed.PendingApprovals.Count > 0
                ? "This reply needs your approval before it can continue. Approvals arrive in a later version of the app."
                : "The assistant returned an empty reply.";
            CanRetry = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsStreaming))]
    private void StopResponse() => _sendCts?.Cancel();

    [RelayCommand]
    private void DismissError()
    {
        ErrorMessage = null;
        CanRetry = false;
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
            Messages.Clear();
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
