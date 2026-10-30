using System.Windows;

namespace VocalEye.Desktop.App.Services;

public sealed class WpfDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message)
    {
        var owner = Application.Current?.MainWindow;
        var result = owner is null
            ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
