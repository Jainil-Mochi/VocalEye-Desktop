using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VocalEye.Desktop.ApiClient;
using VocalEye.Desktop.Core.Interfaces;
using VocalEye.Desktop.Hosting;

namespace VocalEye.Desktop.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IBackendConnection _connection;
    private readonly BackendProcessManager _manager;
    private readonly ILogger<MainViewModel> _logger;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string title = "VocalEye Desktop";
    [ObservableProperty] private string connectionStatus = "Not connected";
    [ObservableProperty] private string backendDetails = "Backend information has not been loaded.";
    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private bool isBusy;

    /// <summary>Phase 2 chat feature, shown once the backend is connected.</summary>
    public ChatViewModel Chat { get; }

    public MainViewModel(
        IBackendConnection connection,
        BackendProcessManager manager,
        ILogger<MainViewModel> logger,
        ChatViewModel chat)
    {
        _connection = connection;
        _manager = manager;
        _logger = logger;
        Chat = chat;
    }

    public async Task InitializeAsync()
    {
        _cts = new CancellationTokenSource();
        await ConnectAsync(_cts.Token);
    }

    [RelayCommand]
    private async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        ConnectionStatus = "Connecting...";
        PublishStatusToChat();

        var loadChat = false;
        try
        {
            var ready = await _manager.StartAsync(cancellationToken);
            IsConnected = ready && await _connection.CheckHealthAsync(cancellationToken);
            ConnectionStatus = IsConnected ? "Connected" : "Backend unavailable";

            if (IsConnected)
            {
                var info = await _connection.GetInfoAsync(cancellationToken);
                BackendDetails = info is null
                    ? "Backend is healthy; optional /api/info endpoint did not return data."
                    : $"{info.Name} | Version: {info.Version} | Environment: {info.Environment}";
                loadChat = true;
            }
            else
            {
                BackendDetails = "Check BaseUrl, backend startup, and the /health endpoint in appsettings.json.";
            }
        }
        catch (OperationCanceledException)
        {
            ConnectionStatus = "Connection cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection check failed.");
            IsConnected = false;
            ConnectionStatus = "Connection failed";
            BackendDetails = ex.Message;
        }
        finally
        {
            IsBusy = false;
            PublishStatusToChat();
        }

        if (loadChat)
        {
            try { await Chat.InitializeAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "Loading conversations failed."); }
        }
    }

    private void PublishStatusToChat()
    {
        Chat.IsConnected = IsConnected;
        Chat.ConnectionStatus = ConnectionStatus;
    }

    public async Task ShutdownAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }
        await _manager.StopAsync();
    }
}
