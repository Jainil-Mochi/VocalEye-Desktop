using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using VocalEye.Desktop.Core.Services;

namespace ChatSpike;

// Simulates SSE: a producer appends a word every ~10 ms into the buffer,
// a UI timer drains it every 50 ms and re-renders the Markdown.
public partial class MainWindow : Window
{
    private const int TotalWords = 2000;

    private readonly StreamingTextBuffer _buffer = new();
    private readonly StringBuilder _text = new();
    private readonly DispatcherTimer _producer = new() { Interval = TimeSpan.FromMilliseconds(10) };
    private readonly DispatcherTimer _renderer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private int _wordsSent;
    private double _worstRenderMs;

    public MainWindow()
    {
        InitializeComponent();
        _producer.Tick += OnProducerTick;
        _renderer.Tick += OnRendererTick;
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        _text.Clear();
        _wordsSent = 0;
        _worstRenderMs = 0;
        _producer.Start();
        _renderer.Start();
    }

    private void OnProducerTick(object? sender, EventArgs e)
    {
        if (_wordsSent >= TotalWords)
        {
            _producer.Stop();
            return;
        }

        // Sprinkle Markdown so rendering has real work to do.
        var piece = _wordsSent % 50 == 0 ? "\n\n## Section " + (_wordsSent / 50) + "\n\n" : "word" + _wordsSent + " ";
        if (_wordsSent % 120 == 0)
        {
            piece += "\n\n```csharp\nvar x = " + _wordsSent + ";\n```\n\n";
        }

        _buffer.Append(piece);
        _wordsSent++;
    }

    private void OnRendererTick(object? sender, EventArgs e)
    {
        if (_buffer.HasPending)
        {
            _text.Append(_buffer.TakeAll());
            var watch = Stopwatch.StartNew();
            Viewer.Document = Markdig.Wpf.Markdown.ToFlowDocument(_text.ToString());
            watch.Stop();
            _worstRenderMs = Math.Max(_worstRenderMs, watch.Elapsed.TotalMilliseconds);
            StatsText.Text = $"words: {_wordsSent}  last render: {watch.Elapsed.TotalMilliseconds:F1} ms  worst: {_worstRenderMs:F1} ms";
        }
        else if (!_producer.IsEnabled)
        {
            _renderer.Stop();
        }
    }
}
