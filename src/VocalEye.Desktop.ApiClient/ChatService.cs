using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VocalEye.Desktop.Core.DTOs;
using VocalEye.Desktop.Core.Exceptions;
using VocalEye.Desktop.Core.Interfaces;

namespace VocalEye.Desktop.ApiClient;

/// <summary>HTTP/SSE implementation of <see cref="IChatService"/>.</summary>
public sealed class ChatService : IChatService
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;

    public ChatService(HttpClient http) => _http = http ?? throw new ArgumentNullException(nameof(http));

    // ---------- Conversations ----------

    public async Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(int take, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 100);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/conversations?take={take}");
        using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        return await ReadListAsync<ConversationSummaryDto>(response, "conversations", cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ConversationMessageDto>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/conversations/{conversationId}/messages");
        using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        return await ReadListAsync<ConversationMessageDto>(response, "messages", cancellationToken).ConfigureAwait(false);
    }

    public async Task RenameConversationAsync(Guid conversationId, string title, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/conversations/{conversationId}")
        {
            Content = JsonContent.Create(new RenameConversationRequestDto { Title = title }, mediaType: null, options: JsonOptions)
        };
        using var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/conversations/{conversationId}");
        using var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    // ---------- Streaming chat ----------

    public async Task<ChatCompletedDto> StreamMessageAsync(ChatRequestDto request, Action<string> onTextChunk, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onTextChunk);

        using var message = new HttpRequestMessage(HttpMethod.Post, "api/chat/stream")
        {
            Content = JsonContent.Create(request, mediaType: null, options: JsonOptions)
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        // ResponseHeadersRead: start reading the body as it arrives instead of buffering it.
        using var response = await SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            await foreach (var evt in SseStreamReader.ReadEventsAsync(stream, cancellationToken).ConfigureAwait(false))
            {
                switch (evt.EventName)
                {
                    case "text":
                        var chunk = Deserialize<TextEventPayload>(evt.Data);
                        if (!string.IsNullOrEmpty(chunk?.Text)) onTextChunk(chunk.Text);
                        break;

                    case "completed":
                        return Deserialize<ChatCompletedDto>(evt.Data)
                               ?? throw new ChatApiException(ChatApiErrorKind.Protocol, "Empty completed event");

                    case "error":
                        var err = TryDeserialize<ProblemPayload>(evt.Data);
                        throw new ChatApiException(ChatApiErrorKind.Stream,
                            err?.Title ?? "The response failed", err?.Detail ?? (err is null ? evt.Data : null));
                }
            }
        }
        catch (IOException ex)
        {
            throw new ChatApiException(ChatApiErrorKind.Network, "Connection lost while streaming", ex.Message, inner: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ChatApiException(ChatApiErrorKind.Network, "Connection lost while streaming", ex.Message, inner: ex);
        }

        throw new ChatApiException(ChatApiErrorKind.Stream, "The response ended unexpectedly",
            "The connection closed before the reply was completed.");
    }

    // ---------- Helpers ----------

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption option, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, option, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new ChatApiException(ChatApiErrorKind.Network, "Cannot reach the VocalEye backend", ex.Message, inner: ex);
        }
        // A client-side HttpClient timeout surfaces as TaskCanceledException without ct being cancelled.
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ChatApiException(ChatApiErrorKind.Network, "The backend did not respond in time", inner: ex);
        }

        if (response.IsSuccessStatusCode) return response;

        try { throw await CreateHttpExceptionAsync(response, ct).ConfigureAwait(false); }
        finally { response.Dispose(); }
    }

    private static async Task<ChatApiException> CreateHttpExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string? title = null, detail = null;
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var problem = TryDeserialize<ProblemPayload>(body);
            title = problem?.Title;
            detail = problem?.Detail;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* body is optional */ }

        title ??= response.ReasonPhrase ?? $"HTTP {(int)response.StatusCode}";
        return new ChatApiException(ChatApiErrorKind.Http, title, detail, response.StatusCode);
    }

    private static async Task<IReadOnlyList<T>> ReadListAsync<T>(HttpResponseMessage response, string wrapperProperty, CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);

            var root = doc.RootElement;
            // Be tolerant: accept a bare array, or an object wrapping the array.
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty(wrapperProperty, out var inner)) root = inner;
                else if (root.TryGetProperty("items", out var items)) root = items;
            }
            if (root.ValueKind != JsonValueKind.Array)
                throw new ChatApiException(ChatApiErrorKind.Protocol, "Unexpected response format", $"Expected a JSON array of {wrapperProperty}.");

            return root.Deserialize<List<T>>(JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new ChatApiException(ChatApiErrorKind.Protocol, "Invalid response from the backend", ex.Message, inner: ex);
        }
    }

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException ex)
        {
            throw new ChatApiException(ChatApiErrorKind.Protocol, "Invalid event from the backend", ex.Message, inner: ex);
        }
    }

    private static T? TryDeserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return default; }
    }

    private sealed class TextEventPayload { public string? Text { get; init; } }
    private sealed class ProblemPayload
    {
        public string? Title { get; init; }
        public string? Detail { get; init; }
        public int? Status { get; init; }
    }
}
