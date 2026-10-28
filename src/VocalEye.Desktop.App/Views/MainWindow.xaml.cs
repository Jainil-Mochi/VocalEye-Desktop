using System.Windows;
using VocalEye.Desktop.App.ViewModels;
namespace VocalEye.Desktop.App.Views;
public partial class MainWindow : Window
{
 private readonly MainViewModel _vm;
 public MainWindow(MainViewModel vm) { InitializeComponent(); _vm=vm; DataContext=vm; Loaded+=OnLoaded; Closed+=OnClosed; }
 private async void OnLoaded(object sender,RoutedEventArgs e) { await _vm.InitializeAsync(); }
 private async void OnClosed(object? sender,EventArgs e) { await _vm.ShutdownAsync(); }
}
