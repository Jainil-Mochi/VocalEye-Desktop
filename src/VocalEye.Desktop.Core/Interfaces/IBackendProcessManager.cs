namespace VocalEye.Desktop.Core.Interfaces;

public interface IBackendProcessManager
{
    bool IsRunning { get; }
    string AuthToken { get; }
    int Port { get; }
    Task<bool> StartBackendAsync(CancellationToken cancellationToken = default);
    Task StopBackendAsync();
}
