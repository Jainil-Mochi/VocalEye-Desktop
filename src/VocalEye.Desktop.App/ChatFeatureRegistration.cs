using Microsoft.Extensions.DependencyInjection;
using VocalEye.Desktop.ApiClient;
using VocalEye.Desktop.App.Services;
using VocalEye.Desktop.App.ViewModels;
using VocalEye.Desktop.Core.Interfaces;

namespace VocalEye.Desktop.App;

public static class ChatFeatureRegistration
{
    /// <summary>
    /// Call once from the existing service registration (same IServiceCollection as Phase 1):
    /// <c>services.AddChatFeature(new Uri(configuration["Backend:BaseUrl"]!));</c>
    /// </summary>
    public static IServiceCollection AddChatFeature(this IServiceCollection services, Uri backendBaseUrl)
    {
        services.AddChatApiClient(backendBaseUrl);

        // ChatService (the typed HttpClient registered above) also provides GET /api/tools.
        services.AddTransient<IToolCatalogService>(sp => (IToolCatalogService)sp.GetRequiredService<IChatService>());

        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<IShellLauncher, WpfShellLauncher>();
        services.AddSingleton<ChatViewModel>();
        return services;
    }
}
