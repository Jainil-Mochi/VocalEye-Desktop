using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace VocalEye.Desktop.App.Controls;

/// <summary>
/// Read-only, selectable Markdown view. Bind <see cref="Markdown"/>; the FlowDocument is rebuilt when it changes
/// (the view model batches streamed text to ~50 ms, so rebuilds stay cheap).
/// </summary>
public sealed class MarkdownViewer : RichTextBox
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown), typeof(string), typeof(MarkdownViewer),
        new PropertyMetadata(string.Empty, (d, _) => ((MarkdownViewer)d).Rebuild()));

    public static readonly DependencyProperty CodeBackgroundProperty = DependencyProperty.Register(
        nameof(CodeBackground), typeof(Brush), typeof(MarkdownViewer),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x0B, 0x10, 0x20)), (d, _) => ((MarkdownViewer)d).Rebuild()));

    public static readonly DependencyProperty CodeForegroundProperty = DependencyProperty.Register(
        nameof(CodeForeground), typeof(Brush), typeof(MarkdownViewer),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xC4, 0xB5, 0xFD)), (d, _) => ((MarkdownViewer)d).Rebuild()));

    public static readonly DependencyProperty LinkForegroundProperty = DependencyProperty.Register(
        nameof(LinkForeground), typeof(Brush), typeof(MarkdownViewer),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)), (d, _) => ((MarkdownViewer)d).Rebuild()));

    public string? Markdown { get => (string?)GetValue(MarkdownProperty); set => SetValue(MarkdownProperty, value); }
    public Brush CodeBackground { get => (Brush)GetValue(CodeBackgroundProperty); set => SetValue(CodeBackgroundProperty, value); }
    public Brush CodeForeground { get => (Brush)GetValue(CodeForegroundProperty); set => SetValue(CodeForegroundProperty, value); }
    public Brush LinkForeground { get => (Brush)GetValue(LinkForegroundProperty); set => SetValue(LinkForegroundProperty, value); }

    public MarkdownViewer()
    {
        IsReadOnly = true;
        IsDocumentEnabled = true; // lets hyperlinks be clicked inside the RichTextBox
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;
        Padding = new Thickness(0);
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Focusable = true;

        AddHandler(System.Windows.Documents.Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate));
        PreviewMouseWheel += ForwardMouseWheel;
    }

    private void Rebuild()
    {
        var theme = new MarkdownTheme(Foreground, CodeBackground, CodeForeground, LinkForeground,
            new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)));
        Document = MarkdownFlowDocumentBuilder.Build(Markdown, theme);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ForegroundProperty) Rebuild();
    }

    private static void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        // Policy: only explicit user clicks on http/https/mailto links are opened, in the default browser.
        if (!MarkdownFlowDocumentBuilder.TryGetSafeUri(e.Uri?.OriginalString, out var uri)) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* no default handler; ignore */ }
    }

    // The RichTextBox would swallow the wheel; pass it to the parent so the chat list scrolls.
    private void ForwardMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        e.Handled = true;
        var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = this };
        (VisualTreeHelper.GetParent(this) as UIElement)?.RaiseEvent(args);
    }
}
