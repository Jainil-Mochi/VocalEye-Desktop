using System.Net;
using FluentAssertions;
using Moq;
using VocalEye.Desktop.App.Services;
using VocalEye.Desktop.App.ViewModels;
using VocalEye.Desktop.Core.DTOs;
using VocalEye.Desktop.Core.Exceptions;
using VocalEye.Desktop.Core.Interfaces;

namespace VocalEye.Desktop.UnitTests;

public class ChatViewModelTests
{
    private readonly Mock<IChatService> _chat = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    public ChatViewModelTests()
    {
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<ConversationSummaryDto>());
        _chat.Setup(c => c.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<ConversationMessageDto>());
        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
    }

    private ChatViewModel Vm() => new(_chat.Object, _dialogs.Object);

    private static ConversationSummaryDto Summary(Guid id, string? title = "T", int minutesAgo = 0) => new()
    {
        ConversationId = id, Title = title, UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo), MessageCount = 1
    };

    private void SetupStream(params string[] chunks) =>
        _chat.Setup(c => c.StreamMessageAsync(It.IsAny<ChatRequestDto>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
             .Returns<ChatRequestDto, Action<string>, CancellationToken>((req, cb, _) =>
             {
                 foreach (var chunk in chunks) cb(chunk);
                 return Task.FromResult(new ChatCompletedDto { ConversationId = req.ConversationId ?? Guid.Empty, Reply = string.Concat(chunks) });
             });

    [Fact]
    public async Task Initialize_loads_conversations_newest_first()
    {
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync([Summary(A, "old", 60), Summary(B, "new", 1)]);
        var vm = Vm();
        await vm.InitializeAsync();
        vm.Conversations.Select(c => c.Id).Should().Equal(B, A);
        vm.HasConversations.Should().BeTrue();
    }

    [Fact]
    public void NewChat_creates_and_selects_draft_and_clears_messages()
    {
        var vm = Vm();
        vm.Messages.Add(new ChatMessageViewModel("user", "old"));
        var focused = false;
        vm.FocusComposerRequested += (_, _) => focused = true;

        vm.NewChatCommand.Execute(null);

        vm.Messages.Should().BeEmpty();
        vm.SelectedConversation.Should().NotBeNull();
        vm.SelectedConversation!.IsDraft.Should().BeTrue();
        vm.Conversations.Should().Contain(vm.SelectedConversation);
        focused.Should().BeTrue();
        _chat.Verify(c => c.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    public async Task Blank_message_does_not_call_api(string draft)
    {
        var vm = Vm();
        vm.DraftMessage = draft;
        await vm.SendMessageCommand.ExecuteAsync(null);
        _chat.Verify(c => c.StreamMessageAsync(It.IsAny<ChatRequestDto>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        vm.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_adds_user_message_streams_chunks_and_finishes()
    {
        SetupStream("Hel", "lo ", "world");
        var vm = Vm();
        vm.DraftMessage = "  Hi there ";

        await vm.SendMessageCommand.ExecuteAsync(null);

        vm.Messages.Should().HaveCount(2);
        vm.Messages[0].IsUser.Should().BeTrue();
        vm.Messages[0].Text.Should().Be("Hi there");
        vm.Messages[1].Text.Should().Be("Hello world");
        vm.Messages[1].IsStreaming.Should().BeFalse();
        vm.IsStreaming.Should().BeFalse();
        vm.DraftMessage.Should().BeEmpty();
        vm.ErrorMessage.Should().BeNull();
        _chat.Verify(c => c.StreamMessageAsync(
            It.Is<ChatRequestDto>(r => r.Message == "Hi there" && r.ConversationId == vm.SelectedConversation!.Id && r.InputMode == "text"),
            It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_refreshes_list_and_keeps_selection_without_reloading_history()
    {
        SetupStream("ok");
        var vm = Vm();
        vm.DraftMessage = "first message";
        await vm.SendMessageCommand.ExecuteAsync(null);
        var id = vm.SelectedConversation!.Id;

        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync([new ConversationSummaryDto { ConversationId = id, Title = null, UpdatedAt = DateTimeOffset.UtcNow, MessageCount = 2 }]);
        vm.DraftMessage = "second";
        await vm.SendMessageCommand.ExecuteAsync(null);

        vm.SelectedConversation!.Id.Should().Be(id);
        vm.SelectedConversation.IsDraft.Should().BeFalse();
        vm.SelectedConversation.DisplayTitle.Should().Be("first message"); // local fallback survives
        vm.Messages.Should().HaveCount(4);
        _chat.Verify(c => c.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Failure_shows_error_enables_retry_and_restores_usable_state()
    {
        _chat.Setup(c => c.StreamMessageAsync(It.IsAny<ChatRequestDto>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new ChatApiException(ChatApiErrorKind.Http, "Model call failed", "x", HttpStatusCode.BadGateway));
        var vm = Vm();
        vm.DraftMessage = "hello";

        await vm.SendMessageCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.CanRetry.Should().BeTrue();
        vm.IsStreaming.Should().BeFalse();
        vm.Messages.Should().ContainSingle().Which.IsUser.Should().BeTrue(); // empty assistant placeholder removed
        vm.RetryCommand.CanExecute(null).Should().BeTrue();

        SetupStream("fine");
        await vm.RetryCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().BeNull();
        vm.Messages.Should().HaveCount(2); // no duplicate user bubble
        vm.Messages[1].Text.Should().Be("fine");
    }

    [Fact]
    public async Task Stop_cancels_request_and_keeps_partial_text_without_error()
    {
        var started = new TaskCompletionSource();
        _chat.Setup(c => c.StreamMessageAsync(It.IsAny<ChatRequestDto>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
             .Returns<ChatRequestDto, Action<string>, CancellationToken>(async (_, cb, ct) =>
             {
                 cb("partial");
                 started.SetResult();
                 await Task.Delay(Timeout.Infinite, ct);
                 return new ChatCompletedDto();
             });
        var vm = Vm();
        vm.DraftMessage = "go";

        var send = vm.SendMessageCommand.ExecuteAsync(null);
        await started.Task;
        vm.IsStreaming.Should().BeTrue();
        vm.StopResponseCommand.Execute(null);
        await send;

        vm.IsStreaming.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
        vm.Messages[1].Text.Should().Be("partial");
        vm.Messages[1].IsIncomplete.Should().BeTrue();
    }

    [Fact]
    public async Task Selecting_conversation_loads_history_in_returned_order()
    {
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([Summary(A)]);
        _chat.Setup(c => c.GetMessagesAsync(A, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ConversationMessageDto { Sequence = 1, Role = "user", Text = "q" },
            new ConversationMessageDto { Sequence = 2, Role = "assistant", Text = "a" }
        ]);
        var vm = Vm();
        await vm.InitializeAsync();

        vm.SelectedConversation = vm.Conversations[0];
        await WaitUntil(() => vm.Messages.Count == 2);

        vm.Messages.Select(m => m.Text).Should().Equal("q", "a");
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Stale_history_response_cannot_overwrite_newer_selection()
    {
        var slow = new TaskCompletionSource<IReadOnlyList<ConversationMessageDto>>();
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([Summary(A), Summary(B)]);
        // A is slow and ignores cancellation (worst case); B answers immediately.
        _chat.Setup(c => c.GetMessagesAsync(A, It.IsAny<CancellationToken>())).Returns(slow.Task);
        _chat.Setup(c => c.GetMessagesAsync(B, It.IsAny<CancellationToken>())).ReturnsAsync(
            [new ConversationMessageDto { Sequence = 1, Role = "user", Text = "from B" }]);
        var vm = Vm();
        await vm.InitializeAsync();

        vm.SelectedConversation = vm.Conversations.First(c => c.Id == A);
        vm.SelectedConversation = vm.Conversations.First(c => c.Id == B);
        await WaitUntil(() => vm.Messages.Count == 1);

        slow.SetResult([new ConversationMessageDto { Sequence = 1, Role = "user", Text = "from A" }]);
        await Task.Delay(100);

        vm.Messages.Should().ContainSingle().Which.Text.Should().Be("from B");
    }

    [Fact]
    public async Task History_load_failure_shows_error_without_crashing()
    {
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([Summary(A)]);
        _chat.Setup(c => c.GetMessagesAsync(A, It.IsAny<CancellationToken>()))
             .ThrowsAsync(new ChatApiException(ChatApiErrorKind.Network, "down"));
        var vm = Vm();
        await vm.InitializeAsync();

        vm.SelectedConversation = vm.Conversations[0];
        await WaitUntil(() => vm.ErrorMessage is not null);

        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Rename_updates_title_only_after_server_confirms()
    {
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([Summary(A, "Old")]);
        var vm = Vm();
        await vm.InitializeAsync();
        var conv = vm.Conversations[0];

        vm.BeginRenameCommand.Execute(conv);
        conv.EditTitle = "C# Learning Notes";
        await vm.RenameConversationCommand.ExecuteAsync(conv);

        _chat.Verify(c => c.RenameConversationAsync(A, "C# Learning Notes", It.IsAny<CancellationToken>()), Times.Once);
        conv.Title.Should().Be("C# Learning Notes");
        conv.IsEditing.Should().BeFalse();
    }

    [Fact]
    public async Task Failed_rename_keeps_old_title_and_editor_open()
    {
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([Summary(A, "Old")]);
        _chat.Setup(c => c.RenameConversationAsync(A, It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new ChatApiException(ChatApiErrorKind.Http, "Method Not Allowed", null, HttpStatusCode.MethodNotAllowed));
        var vm = Vm();
        await vm.InitializeAsync();
        var conv = vm.Conversations[0];

        vm.BeginRenameCommand.Execute(conv);
        conv.EditTitle = "New";
        await vm.RenameConversationCommand.ExecuteAsync(conv);

        conv.Title.Should().Be("Old");
        conv.IsEditing.Should().BeTrue();
        conv.EditTitle.Should().Be("New");
        vm.ErrorMessage.Should().Contain("B4");
    }

    [Fact]
    public async Task Delete_removes_selected_conversation_and_selects_next()
    {
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([Summary(B, "b", 1), Summary(A, "a", 5)]);
        var vm = Vm();
        await vm.InitializeAsync();
        vm.SelectedConversation = vm.Conversations[0]; // B
        await WaitUntil(() => !vm.IsBusy);

        await vm.DeleteConversationCommand.ExecuteAsync(vm.Conversations[0]);

        _chat.Verify(c => c.DeleteConversationAsync(B, It.IsAny<CancellationToken>()), Times.Once);
        vm.Conversations.Select(c => c.Id).Should().Equal(A);
        vm.SelectedConversation!.Id.Should().Be(A);
    }

    [Fact]
    public async Task Delete_last_conversation_shows_empty_state()
    {
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([Summary(A)]);
        var vm = Vm();
        await vm.InitializeAsync();
        vm.SelectedConversation = vm.Conversations[0];

        await vm.DeleteConversationCommand.ExecuteAsync(null);

        vm.Conversations.Should().BeEmpty();
        vm.SelectedConversation.Should().BeNull();
        vm.HasConversations.Should().BeFalse();
        vm.ShowWelcome.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_declined_or_failed_keeps_conversation()
    {
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([Summary(A)]);
        var vm = Vm();
        await vm.InitializeAsync();

        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        await vm.DeleteConversationCommand.ExecuteAsync(vm.Conversations[0]);
        vm.Conversations.Should().HaveCount(1);
        _chat.Verify(c => c.DeleteConversationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        _chat.Setup(c => c.DeleteConversationAsync(A, It.IsAny<CancellationToken>()))
             .ThrowsAsync(new ChatApiException(ChatApiErrorKind.Network, "down"));
        await vm.DeleteConversationCommand.ExecuteAsync(vm.Conversations[0]);
        vm.Conversations.Should().HaveCount(1);
        vm.ErrorMessage.Should().NotBeNull();
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs) throw new TimeoutException("Condition not met.");
            await Task.Delay(10);
        }
    }
}
