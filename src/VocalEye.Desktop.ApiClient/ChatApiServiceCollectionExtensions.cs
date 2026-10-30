using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VocalEye.Desktop.Core.Interfaces;

namespace VocalEye.Desktop.ApiClient;

public static class ChatApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IChatService"/> as a typed HttpClient that uses the Phase 1
    /// <see cref="AuthenticationHandler"/> (bearer token). The client timeout is infinite because
    /// SSE responses are long-lived; cancel via CancellationToken instead.
    /// </summary>
    public static IServiceCollection AddChatApiClient(this IServiceCollection services, Uri backendBaseUrl)
    {
        services.TryAddTransient<AuthenticationHandler>();
        services.AddHttpClient<IChatService, ChatService>(client =>
            {
                client.BaseAddress = backendBaseUrl;
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddHttpMessageHandler<AuthenticationHandler>();
        return services;
    }
}
