using VocalEye.Desktop.Core.Models;

namespace VocalEye.Desktop.Core.Interfaces;

public interface IApiClient
{
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<object> StreamChatAsync(ChatRequestDto request, CancellationToken cancellationToken = default);
}
