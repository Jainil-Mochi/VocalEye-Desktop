using VocalEye.Desktop.Core.Models;
namespace VocalEye.Desktop.Core.Interfaces;
public interface IBackendConnection { Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default); Task<BackendInfo?> GetInfoAsync(CancellationToken cancellationToken = default); }
