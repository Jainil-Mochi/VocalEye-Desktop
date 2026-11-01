using System.Net;
using FluentAssertions;
using Moq;
using VocalEye.Desktop.App.Services;
using VocalEye.Desktop.App.ViewModels;
using VocalEye.Desktop.Core.Approvals;
using VocalEye.Desktop.Core.DTOs;
using VocalEye.Desktop.Core.Exceptions;
using VocalEye.Desktop.Core.Interfaces;

namespace VocalEye.Desktop.UnitTests;

public class ChatViewModelApprovalTests
{
    private readonly Mock<IChatService> _chat = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<IToolCatalogService> _tools = new();
    private readonly Mock<IShellLauncher> _launcher = new();
    private readonly List<ChatRequestDto> _requests = [];
    private readonly Queue<Func<ChatRequestDto, ChatCompletedDto>> _script = new();

    public ChatViewModelApprovalTests()
    {
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<ConversationSummaryDto>());
        _chat.Setup(c => c.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<ConversationMessageDto>());
        _chat.Setup(c => c.GetPendingApprovalsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Array.Empty<PendingApprovalDto>());
        _tools.Setup(t => t.GetToolsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            [new ToolDto { Name = "send_email", Risk = "Destructive" }, new ToolDto { Name = "file_generator", Risk = "Write" }]);
        _chat.Setup(c => c.StreamMessageAsync(It.IsAny<ChatRequestDto>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
             .Returns<ChatRequestDto, Action<string>, CancellationToken>((req, _, _) =>
             {
                 _requests.Add(req);
                 return Task.FromResult(_script.Dequeue()(req));
             });
    }

    private ChatViewModel Vm() => new(_chat.Object, _dialogs.Object, _tools.Object, _launcher.Object);

    private static PendingApprovalDto Pending(string id, string tool, string json = "{}") =>
        new() { ApprovalId = id, ToolName = tool, ArgumentsJson = json };

    private static ChatCompletedDto Waiting(ChatRequestDto req, params PendingApprovalDto[] pending) =>
        new() { ConversationId = req.ConversationId!.Value, Reply = string.Empty, PendingApprovals = [.. pending] };

    private async Task<ChatViewModel> VmWaitingOn(params PendingApprovalDto[] pending)
    {
        _script.Enqueue(req => Waiting(req, pending));
        var vm = Vm();
        await vm.InitializeAsync();
        vm.DraftMessage = "do the thing";
        await vm.SendMessageCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Turn_that_only_asks_for_approval_keeps_the_message_and_shows_cards_without_error()
    {
        var vm = await VmWaitingOn(Pending("c1", "send_email", """{"to":"a@b.com","subject":"s","body":"b"}"""));

        vm.ErrorMessage.Should().BeNull();
        vm.Messages.Should().HaveCount(2);
        var card = vm.Messages[1].Approvals.Should().ContainSingle().Which;
        card.ApprovalId.Should().Be("c1");
        card.Risk.Should().Be(ToolRisk.Destructive);          // from the tool catalog
        card.Summary.Headline.Should().Contain("a@b.com");
        vm.PendingApprovals.Should().ContainSingle();
        vm.HasPendingApprovals.Should().BeTrue();
        vm.PendingApprovalsText.Should().Contain("1 tool action is waiting");
    }

    [Fact]
    public async Task Composer_is_blocked_while_approvals_are_pending()
    {
        var vm = await VmWaitingOn(Pending("c1", "file_generator"));
        vm.DraftMessage = "another message";

        vm.SendMessageCommand.CanExecute(null).Should().BeFalse();
        await vm.SendMessageCommand.ExecuteAsync(null);
        _requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Approve_sends_only_id_and_decision_with_same_conversation_and_shows_results()
    {
        var vm = await VmWaitingOn(Pending("c1", "file_generator"));
        var conversationId = vm.SelectedConversation!.Id;
        _script.Enqueue(req => new ChatCompletedDto
        {
            ConversationId = req.ConversationId!.Value,
            Reply = "Your PDF is ready.",
            ToolCalls = [new ToolCallDto { Name = "file_generator", Success = true, Output = "saved",
                Artifacts = [new ToolArtifactDto { Kind = "file", Value = @"C:\Docs\VocalEye\a.pdf" }] }]
        });

        await vm.ApproveCommand.ExecuteAsync(vm.PendingApprovals[0]);

        var request = _requests[1];
        request.ConversationId.Should().Be(conversationId);
        request.Message.Should().BeNull();
        request.Approvals.Should().ContainSingle().Which.Should().BeEquivalentTo(new ApprovalDecisionDto { ApprovalId = "c1", Approved = true });

        vm.PendingApprovals.Should().BeEmpty();
        vm.Messages[1].Approvals[0].IsResolved.Should().BeTrue();
        vm.Messages[1].Approvals[0].StatusText.Should().Be("Approved");
        vm.Messages.Should().HaveCount(3);                    // user, approval message, result message
        vm.Messages[2].Text.Should().Be("Your PDF is ready.");
        vm.Messages[2].ToolResults.Should().ContainSingle().Which.Artifacts.Should().ContainSingle();
        vm.HasPendingApprovals.Should().BeFalse();
        vm.DraftMessage = "next question";
        vm.SendMessageCommand.CanExecute(null).Should().BeTrue();   // composer unblocked once decisions were sent
    }

    [Fact]
    public async Task Nothing_is_sent_until_every_pending_call_is_decided()
    {
        var vm = await VmWaitingOn(Pending("c1", "file_generator"), Pending("c2", "send_email"));
        _script.Enqueue(req => new ChatCompletedDto { ConversationId = req.ConversationId!.Value, Reply = "ok" });

        await vm.ApproveCommand.ExecuteAsync(vm.PendingApprovals[0]);
        _requests.Should().HaveCount(1);                      // still only the original message
        vm.PendingApprovalsText.Should().Contain("1 left to decide");

        await vm.DenyCommand.ExecuteAsync(vm.PendingApprovals[1]);

        _requests.Should().HaveCount(2);
        _requests[1].Approvals!.Select(a => (a.ApprovalId, a.Approved)).Should().Equal(("c1", true), ("c2", false));
    }

    [Fact]
    public async Task Denied_tool_leaves_the_chat_usable()
    {
        var vm = await VmWaitingOn(Pending("c1", "send_email"));
        _script.Enqueue(req => new ChatCompletedDto { ConversationId = req.ConversationId!.Value, Reply = "Okay, I won't send it." });

        await vm.DenyCommand.ExecuteAsync(vm.PendingApprovals[0]);

        vm.Messages[1].Approvals[0].StatusText.Should().Be("Denied");
        vm.Messages[^1].Text.Should().Contain("won't send");
        vm.ErrorMessage.Should().BeNull();

        _script.Enqueue(req => new ChatCompletedDto { ConversationId = req.ConversationId!.Value, Reply = "next answer" });
        vm.DraftMessage = "something else";
        await vm.SendMessageCommand.ExecuteAsync(null);
        vm.Messages[^1].Text.Should().Be("next answer");
    }

    [Fact]
    public async Task DenyAll_answers_every_call_with_false()
    {
        var vm = await VmWaitingOn(Pending("c1", "file_generator"), Pending("c2", "send_email"));
        _script.Enqueue(req => new ChatCompletedDto { ConversationId = req.ConversationId!.Value, Reply = "ok" });

        await vm.DenyAllCommand.ExecuteAsync(null);

        _requests[1].Approvals.Should().OnlyContain(a => !a.Approved).And.HaveCount(2);
        vm.PendingApprovals.Should().BeEmpty();
    }

    [Fact]
    public async Task Second_round_of_approvals_creates_new_cards()
    {
        var vm = await VmWaitingOn(Pending("c1", "file_generator"));
        _script.Enqueue(req => Waiting(req, Pending("c2", "send_email")));

        await vm.ApproveCommand.ExecuteAsync(vm.PendingApprovals[0]);

        vm.PendingApprovals.Should().ContainSingle().Which.ApprovalId.Should().Be("c2");
        vm.Messages[1].Approvals[0].IsResolved.Should().BeTrue();
        vm.Messages[2].Approvals[0].IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task Failed_submission_keeps_cards_open_and_retry_resends_the_same_decisions()
    {
        var vm = await VmWaitingOn(Pending("c1", "file_generator"));
        _chat.Setup(c => c.StreamMessageAsync(It.IsAny<ChatRequestDto>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new ChatApiException(ChatApiErrorKind.Http, "Model timed out", null, HttpStatusCode.GatewayTimeout));

        await vm.ApproveCommand.ExecuteAsync(vm.PendingApprovals[0]);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.CanRetry.Should().BeTrue();
        vm.PendingApprovals.Should().ContainSingle();         // not resolved: the backend never answered
        vm.Messages.Should().HaveCount(2);                    // empty placeholder removed

        // Backend recovers; Retry sends the same decision (no second user message, no changed arguments).
        var seen = new List<ChatRequestDto>();
        _chat.Setup(c => c.StreamMessageAsync(It.IsAny<ChatRequestDto>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
             .Returns<ChatRequestDto, Action<string>, CancellationToken>((req, _, _) =>
             {
                 seen.Add(req);
                 return Task.FromResult(new ChatCompletedDto { ConversationId = req.ConversationId!.Value, Reply = "done" });
             });
        await vm.RetryCommand.ExecuteAsync(null);

        seen.Should().ContainSingle().Which.Approvals.Should().ContainSingle(a => a.ApprovalId == "c1" && a.Approved);
        vm.PendingApprovals.Should().BeEmpty();
        vm.Messages[^1].Text.Should().Be("done");
    }

    [Fact]
    public async Task Tool_failure_is_shown_in_the_thread_not_as_an_error_banner()
    {
        var vm = await VmWaitingOn(Pending("c1", "send_email"));
        _script.Enqueue(req => new ChatCompletedDto
        {
            ConversationId = req.ConversationId!.Value,
            Reply = "I couldn't send it.",
            ToolCalls = [new ToolCallDto { Name = "send_email", Success = false, Output = "Email is not configured." }]
        });

        await vm.ApproveCommand.ExecuteAsync(vm.PendingApprovals[0]);

        vm.ErrorMessage.Should().BeNull();
        var result = vm.Messages[^1].ToolResults.Should().ContainSingle().Which;
        result.Success.Should().BeFalse();
        result.Output.Should().Contain("not configured");
    }

    [Fact]
    public async Task Stopping_an_approval_turn_closes_the_cards_and_warns_that_tools_may_have_run()
    {
        var vm = await VmWaitingOn(Pending("c1", "send_email"));
        var started = new TaskCompletionSource();
        _chat.Setup(c => c.StreamMessageAsync(It.IsAny<ChatRequestDto>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
             .Returns<ChatRequestDto, Action<string>, CancellationToken>(async (_, _, ct) =>
             {
                 started.SetResult();
                 await Task.Delay(Timeout.Infinite, ct);
                 return new ChatCompletedDto();
             });

        var task = vm.ApproveCommand.ExecuteAsync(vm.PendingApprovals[0]);
        await started.Task;
        vm.StopResponseCommand.Execute(null);
        await task;

        vm.PendingApprovals.Should().BeEmpty();
        vm.CanRetry.Should().BeFalse();                       // never auto-resend: it could run a tool twice
        vm.ErrorMessage.Should().Contain("may already have run");
    }

    [Fact]
    public async Task Resumable_approvals_come_from_the_pending_approvals_endpoint()
    {
        var id = Guid.NewGuid();
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync([new ConversationSummaryDto { ConversationId = id, Title = "t", UpdatedAt = DateTimeOffset.UtcNow }]);
        _chat.Setup(c => c.GetMessagesAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ConversationMessageDto { Sequence = 1, Role = "user", Text = "make a pdf" },
            new ConversationMessageDto { Sequence = 2, Role = "assistant", Text = "", ToolNames = ["file_generator"] }
        ]);
        _chat.Setup(c => c.GetPendingApprovalsAsync(id, It.IsAny<CancellationToken>()))
             .ReturnsAsync([Pending("c9", "file_generator", """{"filename":"x","filetype":"pdf","content":"hi"}""")]);
        var vm = Vm();
        await vm.InitializeAsync();

        vm.SelectedConversation = vm.Conversations[0];
        await WaitUntil(() => vm.PendingApprovals.Count == 1);

        vm.Messages.Should().HaveCount(2);
        vm.Messages[1].Approvals.Should().ContainSingle().Which.ConversationId.Should().Be(id);
        vm.SendMessageCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Pending_approvals_endpoint_failure_does_not_break_history()
    {
        var id = Guid.NewGuid();
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync([new ConversationSummaryDto { ConversationId = id, Title = "t", UpdatedAt = DateTimeOffset.UtcNow }]);
        _chat.Setup(c => c.GetMessagesAsync(id, It.IsAny<CancellationToken>()))
             .ReturnsAsync([new ConversationMessageDto { Sequence = 1, Role = "user", Text = "hi" }]);
        _chat.Setup(c => c.GetPendingApprovalsAsync(id, It.IsAny<CancellationToken>()))
             .ThrowsAsync(new ChatApiException(ChatApiErrorKind.Http, "Not Found", null, HttpStatusCode.NotFound));
        var vm = Vm();
        await vm.InitializeAsync();

        vm.SelectedConversation = vm.Conversations[0];
        await WaitUntil(() => vm.Messages.Count == 1);

        vm.ErrorMessage.Should().BeNull();
        vm.PendingApprovals.Should().BeEmpty();
    }

    [Fact]
    public async Task History_redraws_tool_cards_from_tool_results_and_hides_the_used_tools_placeholder()
    {
        var id = Guid.NewGuid();
        _chat.Setup(c => c.GetConversationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync([new ConversationSummaryDto { ConversationId = id, Title = "t", UpdatedAt = DateTimeOffset.UtcNow }]);
        _chat.Setup(c => c.GetMessagesAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ConversationMessageDto { Sequence = 1, Role = "user", Text = "open example.com" },
            new ConversationMessageDto { Sequence = 2, Role = "assistant", Text = "", ToolNames = ["desktop_command"] },
            new ConversationMessageDto { Sequence = 3, Role = "tool", Text = "", ToolResults =
                [new ToolCallDto { Name = "desktop_command", Success = true, Output = "opened",
                    Artifacts = [new ToolArtifactDto { Kind = "url", Value = "https://example.com" }] }] },
            new ConversationMessageDto { Sequence = 4, Role = "assistant", Text = "Opened it." }
        ]);
        var vm = Vm();
        await vm.InitializeAsync();

        vm.SelectedConversation = vm.Conversations[0];
        await WaitUntil(() => vm.Messages.Count == 3);

        vm.Messages.Select(m => m.Role).Should().Equal("user", "tool", "assistant");
        var artifact = vm.Messages[1].ToolResults.Should().ContainSingle().Which.Artifacts.Should().ContainSingle().Which;
        artifact.IsUrl.Should().BeTrue();
        artifact.Path.Should().Be("https://example.com");
        artifact.OpenCommand.Execute(null);
        _launcher.Verify(l => l.OpenUrl("https://example.com"), Times.Once);
    }

    [Fact]
    public async Task Artifact_open_and_reveal_go_through_the_launcher_and_errors_surface()
    {
        var vm = await VmWaitingOn(Pending("c1", "file_generator"));
        _script.Enqueue(req => new ChatCompletedDto
        {
            ConversationId = req.ConversationId!.Value,
            Reply = "done",
            ToolCalls = [new ToolCallDto { Name = "file_generator", Success = true, Output = @"Saved C:\Docs\VocalEye\a.pdf" }] // text fallback
        });
        await vm.ApproveCommand.ExecuteAsync(vm.PendingApprovals[0]);
        var artifact = vm.Messages[^1].ToolResults[0].Artifacts.Should().ContainSingle().Which;
        artifact.Path.Should().Be(@"C:\Docs\VocalEye\a.pdf");

        artifact.OpenCommand.Execute(null);
        artifact.RevealCommand.Execute(null);
        _launcher.Verify(l => l.OpenPath(@"C:\Docs\VocalEye\a.pdf"), Times.Once);
        _launcher.Verify(l => l.RevealPath(@"C:\Docs\VocalEye\a.pdf"), Times.Once);
        vm.ErrorMessage.Should().BeNull();

        _launcher.Setup(l => l.OpenPath(It.IsAny<string>())).Returns("That file no longer exists.");
        artifact.OpenCommand.Execute(null);
        vm.ErrorMessage.Should().Be("That file no longer exists.");
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
