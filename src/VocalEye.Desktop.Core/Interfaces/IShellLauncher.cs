namespace VocalEye.Desktop.Core.Interfaces;

/// <summary>
/// Opens generated files, folders and links on the user's PC. Every method returns null on success,
/// or a short user-facing error. Implementations must apply <see cref="Artifacts.ArtifactPolicy"/>.
/// </summary>
public interface IShellLauncher
{
    string? OpenPath(string path);
    string? RevealPath(string path);
    string? OpenUrl(string url);
}
