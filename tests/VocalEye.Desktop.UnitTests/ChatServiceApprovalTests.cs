using System.Net;
using FluentAssertions;
using VocalEye.Desktop.ApiClient;
using VocalEye.Desktop.Core.DTOs;
using VocalEye.Desktop.Core.Exceptions;

namespace VocalEye.Desktop.UnitTests;

public class ChatServiceApprovalTests
{
    private static readonly Guid Id = Guid.Parse("7c444cfe-9c61-46d0-81df-7b4b08408d07");

    private static (ChatService Service, StubHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler((r, _) => Task.FromResult(respond(r)));
        return (new ChatService(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") }), handler);
    }

    [Fact]
    public async Task Approval_request_sends_only_ids_and_decisions_without_message()
    {
        var (svc, h) = Create(_ => Http.Sse($"event: completed\ndata: {{\"conversationId\":\"{Id}\",\"reply\":\"Done\"}}\n\n"));
        await svc.StreamMessageAsync(new ChatRequestDto
        {
            ConversationId = Id,
            Approvals = [new ApprovalDecisionDto { ApprovalId = "call_1", Approved = true }, new ApprovalDecisionDto { ApprovalId = "call_2", Approved = false }]
        }, _ => { }, default);

        var body = h.Calls[0].Body!;
        body.Should().Contain("\"approvals\":[{\"approvalId\":\"call_1\",\"approved\":true},{\"approvalId\":\"call_2\",\"approved\":false}]");
        body.Should().NotContain("\"message\"").And.NotContain("\"model\"");
    }

    [Fact]
    public async Task Completed_event_deserializes_tool_calls_artifacts_and_pending_approvals()
    {
        const string completed = """
            {"conversationId":"7c444cfe-9c61-46d0-81df-7b4b08408d07","reply":"",
             "modelUsed":{"provider":"groq","model":"llama-3.3-70b-versatile"},
             "toolCalls":[{"name":"file_generator","argumentsJson":"{}","success":true,"output":"saved",
                           "artifacts":[{"kind":"file","value":"C:\\Docs\\a.pdf"}]},
                          {"name":"send_email","success":false,"output":"not configured","artifacts":null}],
             "pendingApprovals":[{"approvalId":"call_461098","toolName":"file_generator","argumentsJson":"{\"filename\":\"x\"}"}]}
            """;
        var (svc, _) = Create(_ => Http.Sse("event: completed\ndata: " + completed.ReplaceLineEndings(" ") + "\n\n"));

        var done = await svc.StreamMessageAsync(new ChatRequestDto { Message = "x" }, _ => { }, default);

        done.ToolCalls.Should().HaveCount(2);
        done.ToolCalls[0].Artifacts.Should().ContainSingle().Which.Value.Should().Be(@"C:\Docs\a.pdf");
        done.ToolCalls[1].Success.Should().BeFalse();
        done.ToolCalls[1].Artifacts.Should().BeEmpty(); // null artifacts tolerated
        done.PendingApprovals.Should().ContainSingle().Which.ApprovalId.Should().Be("call_461098");
        done.ModelUsed!.Model.Should().Be("llama-3.3-70b-versatile");
    }

    [Fact]
    public async Task Unknown_approval_id_returns_the_backend_detail()
    {
        var (svc, _) = Create(_ => Http.Json("""{"title":"Invalid request","detail":"No pending approval with id call_x","status":400}""", HttpStatusCode.BadRequest));
        var ex = (await FluentActions.Awaiting(() => svc.StreamMessageAsync(
                new ChatRequestDto { ConversationId = Id, Approvals = [new ApprovalDecisionDto { ApprovalId = "call_x", Approved = true }] }, _ => { }, default))
            .Should().ThrowAsync<ChatApiException>()).Which;
        ex.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Detail.Should().Contain("No pending approval");
    }

    [Fact]
    public async Task GetTools_deserializes_risk()
    {
        var (svc, h) = Create(_ => Http.Json("""[{"name":"send_email","description":"d","risk":"Destructive"},{"name":"get_current_time","description":"t","risk":"ReadOnly"}]"""));
        var tools = await svc.GetToolsAsync(default);
        tools.Select(t => t.Risk).Should().Equal("Destructive", "ReadOnly");
        h.Calls[0].Request.RequestUri!.AbsolutePath.Should().Be("/api/tools");
    }

    [Fact]
    public async Task Messages_carry_tool_results_with_artifacts()
    {
        var (svc, _) = Create(_ => Http.Json("""
            [{"sequence":1,"role":"assistant","text":"","toolNames":["file_generator"]},
             {"sequence":2,"role":"tool","text":"","toolNames":[],
              "toolResults":[{"name":"file_generator","argumentsJson":"{}","success":true,"output":"saved",
                              "artifacts":[{"kind":"file","value":"C:\\Docs\\a.pdf"}]}]}]
            """));
        var messages = await svc.GetMessagesAsync(Id, default);

        messages[0].ToolResults.Should().BeEmpty();
        var result = messages[1].ToolResults.Should().ContainSingle().Which;
        result.ResolvedName.Should().Be("file_generator");
        result.Artifacts.Should().ContainSingle().Which.Value.Should().Be(@"C:\Docs\a.pdf");
    }

    [Fact]
    public async Task PendingApprovals_endpoint_returns_waiting_calls()
    {
        var (svc, h) = Create(_ => Http.Json("""[{"approvalId":"c1","toolName":"file_generator","argumentsJson":"{}"}]"""));
        var pending = await svc.GetPendingApprovalsAsync(Id, default);

        pending.Should().ContainSingle().Which.ApprovalId.Should().Be("c1");
        h.Calls[0].Request.RequestUri!.AbsolutePath.Should().Be($"/api/conversations/{Id}/pending-approvals");
    }

    [Fact]
    public async Task PendingApprovals_empty_list_and_unknown_conversation()
    {
        var (empty, _) = Create(_ => Http.Json("[]"));
        (await empty.GetPendingApprovalsAsync(Id, default)).Should().BeEmpty();

        var (missing, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var ex = (await FluentActions.Awaiting(() => missing.GetPendingApprovalsAsync(Id, default)).Should().ThrowAsync<ChatApiException>()).Which;
        ex.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
