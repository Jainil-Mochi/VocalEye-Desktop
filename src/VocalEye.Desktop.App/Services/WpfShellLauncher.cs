using System.Diagnostics;
using VocalEye.Desktop.Core.Artifacts;
using VocalEye.Desktop.Core.Interfaces;
using System.IO;

namespace VocalEye.Desktop.App.Services;

/// <summary>Opens results in Explorer / the default app, after <see cref="ArtifactPolicy"/> approves the target.</summary>
public sealed class WpfShellLauncher : IShellLauncher
{
    public string? OpenPath(string path)
    {
        if (Directory.Exists(path))
        {
            if (!ArtifactPolicy.IsSafeFolderPath(path, out var folderReason)) return folderReason;
            return Start(new ProcessStartInfo("explorer.exe", Quote(path)) { UseShellExecute = true });
        }

        if (!ArtifactPolicy.IsSafeFilePath(path, out var reason)) return reason;
        if (!File.Exists(path)) return "That file no longer exists. It may have been moved or deleted.";
        return Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public string? RevealPath(string path)
    {
        if (Directory.Exists(path))
            return OpenPath(path);

        if (!ArtifactPolicy.IsSafeFolderPath(path, out var reason)) return reason; // reveal never launches the file itself
        if (!File.Exists(path)) return "That file no longer exists. It may have been moved or deleted.";
        return Start(new ProcessStartInfo("explorer.exe", $"/select,{Quote(path)}") { UseShellExecute = true });
    }

    public string? OpenUrl(string url)
    {
        if (!ArtifactPolicy.IsSafeUrl(url, out var uri, out var reason)) return reason;
        return Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private static string Quote(string path) => "\"" + path.Replace("\"", string.Empty) + "\"";

    private static string? Start(ProcessStartInfo info)
    {
        try
        {
            using var _ = Process.Start(info);
            return null;
        }
        catch (Exception ex)
        {
            return "Couldn't open that: " + ex.Message;
        }
    }
}
