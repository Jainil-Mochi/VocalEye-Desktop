using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using VocalEye.Desktop.Core.Interfaces;
using VocalEye.Desktop.Core.Models;

namespace VocalEye.Desktop.ApiClient.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IBackendProcessManager _processManager;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ApiClient(HttpClient httpClient, IBackendProcessManager processManager)
    {
        _httpClient = httpClient;
        _processManager = processManager;
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"http://127.0.0.1:{_processManager.Port}/health", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async IAsyncEnumerable<object> StreamChatAsync(ChatRequestDto request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var reqMsg = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{_processManager.Port}/api/chat/stream")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        if (!string.IsNullOrEmpty(_processManager.AuthToken))
        {
            reqMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _processManager.AuthToken);
        }

        using var response = await _httpClient.SendAsync(reqMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        string? currentEvent = null;
        string? line;
        while (!cancellationToken.IsCancellationRequested && (line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("event:"))
            {
                currentEvent = line.Replace("event:", "").Trim();
            }
            else if (line.StartsWith("data:"))
            {
                var dataJson = line.Replace("data:", "").Trim();
                if (currentEvent == "text")
                {
                    var textObj = JsonSerializer.Deserialize<SseTextEventDto>(dataJson, JsonOptions);
                    if (textObj != null)
                    {
                        yield return textObj;
                    }
                }
                else if (currentEvent == "completed")
                {
                    var compObj = JsonSerializer.Deserialize<ChatResponseDto>(dataJson, JsonOptions);
                    if (compObj != null)
                    {
                        yield return compObj;
                    }
                }
                else if (currentEvent == "error")
                {
                    var errObj = JsonSerializer.Deserialize<SseErrorEventDto>(dataJson, JsonOptions);
                    if (errObj != null)
                    {
                        yield return errObj;
                    }
                }
            }
        }
    }
}
