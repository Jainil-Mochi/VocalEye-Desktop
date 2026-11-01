using CommunityToolkit.Mvvm.Input;
using VocalEye.Desktop.Core.Artifacts;
using VocalEye.Desktop.Core.DTOs;
using VocalEye.Desktop.Core.Interfaces;

namespace VocalEye.Desktop.App.ViewModels;

/// <summary>A file, folder or link produced by a tool, with explicit Open / Show in folder actions.</summary>
public partial class ArtifactViewModel
{
    private readonly IShellLauncher? _launcher;
    private readonly Action<string> _reportError;

    public ArtifactViewModel(ToolArtifactDto dto, IShellLauncher? launcher, Action<string> reportError)
    {
        Kind = dto.Kind.ToLowerInvariant();
        Path = dto.ResolvedValue;
        _launcher = launcher;
        _reportError = reportError;
    }

    public string Kind { get; }
    public string Path { get; }
    public bool IsUrl => Kind == ArtifactExtractor.KindUrl;
    public bool CanReveal => !IsUrl;
    public string OpenLabel => Kind switch
    {
        ArtifactExtractor.KindFolder => "Open folder",
        ArtifactExtractor.KindUrl => "Open link",
        _ => "Open"
    };
    public string DisplayName => IsUrl ? Path : System.IO.Path.GetFileName(Path.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : Path;

    [RelayCommand]
    private void Open() => Run(l => IsUrl ? l.OpenUrl(Path) : l.OpenPath(Path));

    [RelayCommand]
    private void Reveal() => Run(l => l.RevealPath(Path));

    private void Run(Func<IShellLauncher, string?> action)
    {
        if (_launcher is null)
        {
            _reportError("Opening files isn't available in this build.");
            return;
        }
        var error = action(_launcher);
        if (error is not null) _reportError(error);
    }
}
