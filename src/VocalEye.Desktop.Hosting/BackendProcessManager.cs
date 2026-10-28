using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VocalEye.Desktop.Core.Interfaces;
namespace VocalEye.Desktop.Hosting;
public sealed class BackendProcessManager : IAsyncDisposable
{
 private readonly BackendProcessOptions _options; private readonly IBackendConnection _connection; private readonly ILogger<BackendProcessManager> _logger; private Process? _process;
 public BackendProcessManager(IOptions<BackendProcessOptions> options, IBackendConnection connection, ILogger<BackendProcessManager> logger) { _options = options.Value; _connection = connection; _logger = logger; }
 public async Task<bool> StartAsync(CancellationToken token = default)
 {
  if (!_options.LaunchBackend) return await _connection.CheckHealthAsync(token);
  if (_process is { HasExited: false }) return await WaitForHealthAsync(token);
  if (string.IsNullOrWhiteSpace(_options.ProjectPath) || !File.Exists(_options.ProjectPath)) { _logger.LogWarning("Invalid backend project path: {Path}", _options.ProjectPath); return false; }
  var psi = new ProcessStartInfo { FileName = "dotnet", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(_options.ProjectPath))! };
  psi.ArgumentList.Add("run"); psi.ArgumentList.Add("--project"); psi.ArgumentList.Add(Path.GetFullPath(_options.ProjectPath)); psi.ArgumentList.Add("--no-launch-profile"); psi.ArgumentList.Add("--"); psi.ArgumentList.Add("--urls"); psi.ArgumentList.Add(_options.BaseUrl);
  try { _process = new Process { StartInfo = psi, EnableRaisingEvents = true }; _process.OutputDataReceived += (_,e) => { if(e.Data is not null) _logger.LogInformation("[Backend] {Line}", e.Data); }; _process.ErrorDataReceived += (_,e) => { if(e.Data is not null) _logger.LogWarning("[Backend] {Line}", e.Data); }; if(!_process.Start()) return false; _process.BeginOutputReadLine(); _process.BeginErrorReadLine(); return await WaitForHealthAsync(token); }
  catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { _logger.LogError(ex, "Failed to start backend."); return false; }
 }
 private async Task<bool> WaitForHealthAsync(CancellationToken token) { var sw=Stopwatch.StartNew(); var timeout=TimeSpan.FromSeconds(Math.Clamp(_options.StartupTimeoutSeconds,1,300)); while(sw.Elapsed<timeout && !token.IsCancellationRequested) { if(_process is { HasExited:true }) return false; if(await _connection.CheckHealthAsync(token)) return true; await Task.Delay(750,token); } return false; }
 public async Task StopAsync() { var p=_process; _process=null; if(p is null) return; try { if(!p.HasExited) p.Kill(true); await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch(InvalidOperationException) {} catch(System.ComponentModel.Win32Exception ex) { _logger.LogDebug(ex,"Process already stopped."); } catch(TimeoutException) { _logger.LogWarning("Timed out stopping backend."); } finally { p.Dispose(); } }
 public async ValueTask DisposeAsync() => await StopAsync();
}
