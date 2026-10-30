using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VocalEye.Desktop.App.ViewModels;

namespace VocalEye.Desktop.App.Views;

public partial class ChatView : UserControl
{
    private ChatViewModel? _vm;

    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => Composer.Focus();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.FocusComposerRequested -= OnFocusComposer;
            _vm.ScrollToEndRequested -= OnScrollToEnd;
        }
        _vm = e.NewValue as ChatViewModel;
        if (_vm is not null)
        {
            _vm.FocusComposerRequested += OnFocusComposer;
            _vm.ScrollToEndRequested += OnScrollToEnd;
        }
    }

    private void OnFocusComposer(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => { Composer.Focus(); Keyboard.Focus(Composer); }, System.Windows.Threading.DispatcherPriority.Input);

    private void OnScrollToEnd(object? sender, EventArgs e)
    {
        // Only follow the stream if the user hasn't scrolled up to read earlier text.
        var nearBottom = MessageScroller.ScrollableHeight - MessageScroller.VerticalOffset < 120;
        if (nearBottom)
            Dispatcher.BeginInvoke(MessageScroller.ScrollToEnd, System.Windows.Threading.DispatcherPriority.Background);
    }

    // Enter sends, Shift+Enter inserts a new line.
    private void Composer_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
        if (_vm?.SendMessageCommand.CanExecute(null) == true)
            _vm.SendMessageCommand.Execute(null);
        e.Handled = true;
    }

    // Inline rename editor
    private void RenameBox_Loaded(object sender, RoutedEventArgs e) { }

    private void RenameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox box && box.IsVisible)
            box.Dispatcher.BeginInvoke(() => { box.Focus(); box.SelectAll(); }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ConversationSummaryViewModel conversation } || _vm is null) return;
        if (e.Key == Key.Enter) { _vm.RenameConversationCommand.Execute(conversation); e.Handled = true; }
        else if (e.Key == Key.Escape) { _vm.CancelRenameCommand.Execute(conversation); e.Handled = true; }
    }
}
