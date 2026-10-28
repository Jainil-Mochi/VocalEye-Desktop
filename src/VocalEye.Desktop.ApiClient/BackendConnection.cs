using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using VocalEye.Desktop.Core.Interfaces;
using VocalEye.Desktop.Core.Models;
namespace VocalEye.Desktop.ApiClient;
public sealed class BackendConnection : IBackendConnection
{
 private readonly HttpClient _client; private readonly BackendApiOptions _options;
 public BackendConnection(HttpClient client, IOptions<BackendApiOptions> options) { _client = client; _options = options.Value; _client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/"); _client.Timeout = TimeSpan.FromSeconds(5); }
 public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default) { try { using var req = CreateRequest(_options.HealthPath); using var res = await _client.SendAsync(req, cancellationToken); return res.IsSuccessStatusCode; } catch (HttpRequestException) { return false; } catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; } catch (UriFormatException) { return false; } }
 public async Task<BackendInfo?> GetInfoAsync(CancellationToken cancellationToken = default) { try { using var req = CreateRequest(_options.InfoPath); using var res = await _client.SendAsync(req, cancellationToken); if (!res.IsSuccessStatusCode) return null; return await res.Content.ReadFromJsonAsync<BackendInfo>(cancellationToken: cancellationToken); } catch (HttpRequestException) { return null; } catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; } catch (System.Text.Json.JsonException) { return null; } }
 private HttpRequestMessage CreateRequest(string path) { var req = new HttpRequestMessage(HttpMethod.Get, path); if (!string.IsNullOrWhiteSpace(_options.AccessToken)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken); return req; }
}
