using System.Text;

namespace VocalEye.Desktop.Core.Streaming;

/// <summary>
/// Collects many tiny streamed chunks and hands them to <c>flush</c> in batches (default 50 ms),
/// so the UI is notified far less often. <see cref="DisposeAsync"/> stops the loop and flushes the rest.
/// </summary>
public sealed class StreamingTextBatcher : IAsyncDisposable
{
    private readonly Action<string> _flush;
    private readonly StringBuilder _buffer = new();
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public StreamingTextBatcher(Action<string> flush, TimeSpan? interval = null)
    {
        _flush = flush ?? throw new ArgumentNullException(nameof(flush));
        _loop = Task.Run(() => RunAsync(interval ?? TimeSpan.FromMilliseconds(50)));
    }

    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        lock (_gate) _buffer.Append(text);
    }

    private async Task RunAsync(TimeSpan interval)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
                Drain();
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    private void Drain()
    {
        string text;
        lock (_gate)
        {
            if (_buffer.Length == 0) return;
            text = _buffer.ToString();
            _buffer.Clear();
        }
        _flush(text);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _loop.ConfigureAwait(false); } catch { /* flush callback faults are ignored on shutdown */ }
        Drain(); // final flush, after the loop has stopped so ordering is preserved
        _cts.Dispose();
    }
}
