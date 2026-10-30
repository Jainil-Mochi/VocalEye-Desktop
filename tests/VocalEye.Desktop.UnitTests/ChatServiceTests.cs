using System.Net;
using FluentAssertions;
using VocalEye.Desktop.ApiClient;
using VocalEye.Desktop.Core.DTOs;
using VocalEye.Desktop.Core.Exceptions;

namespace VocalEye.Desktop.UnitTests;

public class ChatServiceTests
{
    private static (ChatService Service, StubHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler((r, _) => Task.FromResult(respond(r)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") };
        return (new ChatService(http), handler);
    }

    private static readonly Guid Id = Guid.Parse("7c444cfe-9c61-46d0-81df-7b4b08408d07");

    [Fact]
    public async Task GetConversations_deserializes_camelCase_and_uses_take()
    {
        var (svc, h) = Create(_ => Http.Json($$"""[{"conversationId":"{{Id}}","title":"Hi","model":"m","updatedAt":"2026-10-01T10:00:00+00:00","messageCount":3}]"""));
        var list = await svc.GetConversationsAsync(25, default);

        list.Should().ContainSingle();
        list[0].ConversationId.Should().Be(Id);
        list[0].Title.Should().Be("Hi");
        list[0].MessageCount.Should().Be(3);
        h.Calls[0].Request.RequestUri!.PathAndQuery.Should().Be("/api/conversations?take=25");
    }

    [Fact]
    public async Task GetConversations_without_title_is_allowed()
    {
        var (svc, _) = Create(_ => Http.Json($$"""[{"conversationId":"{{Id}}","model":"m","updatedAt":"2026-10-01T10:00:00Z","messageCount":1}]"""));
        (await svc.GetConversationsAsync(50, default))[0].Title.Should().BeNull();
    }

    [Fact]
    public async Task GetMessages_deserializes_in_returned_order()
    {
        var (svc, h) = Create(_ => Http.Json("""[{"sequence":1,"role":"user","text":"a","toolNames":[]},{"sequence":2,"role":"assistant","text":"b","toolNames":["search"]}]"""));
        var msgs = await svc.GetMessagesAsync(Id, default);

        msgs.Select(m => m.Text).Should().Equal("a", "b");
        msgs[1].ToolNames.Should().Equal("search");
        h.Calls[0].Request.RequestUri!.AbsolutePath.Should().Be($"/api/conversations/{Id}/messages");
    }

    [Fact]
    public async Task Stream_sends_camelCase_json_and_accept_header()
    {
        var (svc, h) = Create(_ => Http.Sse("event: completed\ndata: {\"conversationId\":\"" + Id + "\",\"reply\":\"ok\"}\n\n"));
        await svc.StreamMessageAsync(new ChatRequestDto { ConversationId = Id, Message = "hello" }, _ => { }, default);

        var (req, body) = h.Calls[0];
        req.Method.Should().Be(HttpMethod.Post);
        req.RequestUri!.AbsolutePath.Should().Be("/api/chat/stream");
        req.Headers.Accept.Should().Contain(a => a.MediaType == "text/event-stream");
        body.Should().Contain("\"conversationId\"").And.Contain("\"message\":\"hello\"").And.Contain("\"inputMode\":\"text\"");
    }

    [Fact]
    public async Task Stream_appends_text_chunks_in_order_and_returns_completed()
    {
        const string sse =
            "event: text\ndata: {\"text\":\"Hel\"}\n\n" +
            "event: text\ndata: {\"text\":\"lo\"}\n\n" +
            "event: completed\ndata: {\"conversationId\":\"7c444cfe-9c61-46d0-81df-7b4b08408d07\",\"reply\":\"Hello\",\"modelUsed\":{\"provider\":\"p\",\"model\":\"m\"},\"toolCalls\":[],\"pendingApprovals\":[]}\n\n";
        var (svc, _) = Create(_ => Http.Sse(sse, chunkSize: 5));

        var chunks = new List<string>();
        var done = await svc.StreamMessageAsync(new ChatRequestDto { Message = "x" }, chunks.Add, default);

        chunks.Should().Equal("Hel", "lo");
        done.Reply.Should().Be("Hello");
        done.ConversationId.Should().Be(Id);
        done.ModelUsed!.Provider.Should().Be("p");
    }

    [Fact]
    public async Task Stream_error_event_becomes_exception()
    {
        var (svc, _) = Create(_ => Http.Sse("event: error\ndata: {\"title\":\"Model call failed\",\"detail\":\"boom\"}\n\n"));
        var ex = (await FluentActions.Awaiting(() => svc.StreamMessageAsync(new ChatRequestDto { Message = "x" }, _ => { }, default))
            .Should().ThrowAsync<ChatApiException>()).Which;

        ex.Kind.Should().Be(ChatApiErrorKind.Stream);
        ex.Title.Should().Be("Model call failed");
        ex.Detail.Should().Be("boom");
    }

    [Fact]
    public async Task Stream_ending_without_completed_is_an_error()
    {
        var (svc, _) = Create(_ => Http.Sse("event: text\ndata: {\"text\":\"a\"}\n\n"));
        var ex = (await FluentActions.Awaiting(() => svc.StreamMessageAsync(new ChatRequestDto { Message = "x" }, _ => { }, default))
            .Should().ThrowAsync<ChatApiException>()).Which;
        ex.Kind.Should().Be(ChatApiErrorKind.Stream);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Http_errors_surface_problem_title_detail_and_status(HttpStatusCode status)
    {
        var (svc, _) = Create(_ => Http.Json("""{"title":"T","detail":"D","status":0}""", status));
        var ex = (await FluentActions.Awaiting(() => svc.StreamMessageAsync(new ChatRequestDto { Message = "x" }, _ => { }, default))
            .Should().ThrowAsync<ChatApiException>()).Which;

        ex.StatusCode.Should().Be(status);
        ex.Title.Should().Be("T");
        ex.Detail.Should().Be("D");
        ex.IsTransient.Should().Be(status is HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout);
    }

    [Fact]
    public async Task Http_error_without_problem_body_still_throws_chat_exception()
    {
        var (svc, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var ex = (await FluentActions.Awaiting(() => svc.GetMessagesAsync(Id, default)).Should().ThrowAsync<ChatApiException>()).Which;
        ex.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Backend_unavailable_maps_to_network_error()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("refused"));
        var svc = new ChatService(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") });
        var ex = (await FluentActions.Awaiting(() => svc.GetConversationsAsync(50, default)).Should().ThrowAsync<ChatApiException>()).Which;
        ex.Kind.Should().Be(ChatApiErrorKind.Network);
    }

    [Fact]
    public async Task Cancelled_token_throws_operation_canceled()
    {
        var handler = new StubHandler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); });
        var svc = new ChatService(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") });
        using var cts = new CancellationTokenSource(50);
        await FluentActions.Awaiting(() => svc.StreamMessageAsync(new ChatRequestDto { Message = "x" }, _ => { }, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Rename_uses_patch_with_title_body()
    {
        var (svc, h) = Create(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        await svc.RenameConversationAsync(Id, "C# Learning Notes", default);

        var (req, body) = h.Calls[0];
        req.Method.Should().Be(HttpMethod.Patch);
        req.RequestUri!.AbsolutePath.Should().Be($"/api/conversations/{Id}");
        body.Should().Be("{\"title\":\"C# Learning Notes\"}");
    }

    [Fact]
    public async Task Delete_uses_delete_method_and_path()
    {
        var (svc, h) = Create(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        await svc.DeleteConversationAsync(Id, default);

        h.Calls[0].Request.Method.Should().Be(HttpMethod.Delete);
        h.Calls[0].Request.RequestUri!.AbsolutePath.Should().Be($"/api/conversations/{Id}");
    }
}
