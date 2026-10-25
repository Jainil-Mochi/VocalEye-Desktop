using CommunityToolkit.Mvvm.ComponentModel;

namespace VocalEye.Desktop.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "VocalEye Desktop";

    [ObservableProperty]
    private string status = "Phase 0 - Foundations";
}
