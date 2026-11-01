using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VocalEye.Desktop.Core.Approvals;
using VocalEye.Desktop.Core.Artifacts;
using VocalEye.Desktop.Core.DTOs;
using VocalEye.Desktop.Core.Interfaces;

namespace VocalEye.Desktop.App.ViewModels;

/// <summary>What a tool did. Failures are shown in the thread with their reason; they are not exceptions.</summary>
public partial class ToolResultViewModel : ObservableObject
{
    public ToolResultViewModel(ToolCallDto call, IShellLauncher? launcher, Action<string> reportError)
    {
        ToolName = call.ResolvedName;
        Success = call.Success;
        Output = call.Output ?? string.Empty;
        foreach (var artifact in ArtifactExtractor.Resolve(call))
            Artifacts.Add(new ArtifactViewModel(artifact, launcher, reportError));
    }

    public string ToolName { get; }
    public string DisplayName => ToolDisplay.Name(ToolName);
    public bool Success { get; }
    public string StatusText => Success ? "Done" : "Failed";
    public string Output { get; }
    public bool HasOutput => !string.IsNullOrWhiteSpace(Output);
    public ObservableCollection<ArtifactViewModel> Artifacts { get; } = [];
    public bool HasArtifacts => Artifacts.Count > 0;
}
