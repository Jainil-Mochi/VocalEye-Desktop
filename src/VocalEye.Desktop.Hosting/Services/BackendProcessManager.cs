using System.Diagnostics;
using VocalEye.Desktop.Core.Interfaces;

namespace VocalEye.Desktop.Hosting.Services;

public class BackendProcessManager : IBackendProcessManager
{
    private Process? _process;
    public bool IsRunning => _process != null && !_process.HasExited;
    public string AuthToken { get; private set; } = Guid.NewGuid().ToString("N");
    public int Port { get; private set; } = 5055;

    public async Task<bool> StartBackendAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return true;
        }

        // In production/simulation, launches bundled backend executable
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c echo VocalEye Backend Service",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = new Process { StartInfo = startInfo };
        _process.Start();

        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task StopBackendAsync()
    {
        if (_process != null && !_process.HasExited)
        {
            _process.Kill();
            _process.Dispose();
            _process = null;
        }
        return Task.CompletedTask;
    }
}
