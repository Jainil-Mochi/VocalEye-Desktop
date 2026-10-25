using System.Windows;
using VocalEye.Desktop.App.ViewModels;

namespace VocalEye.Desktop.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
