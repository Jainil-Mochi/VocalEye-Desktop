using System.Text;

namespace VocalEye.Desktop.Core.Services;

/// <summary>
/// Collects streamed text pieces so the UI can append them in batches
/// (the plan uses 50 ms) instead of re-rendering on every SSE event.
/// Safe to call Append from a background thread.
/// </summary>
public sealed class StreamingTextBuffer
{
    private readonly StringBuilder _builder = new();
    private readonly object _gate = new();

    public bool HasPending
    {
        get
        {
            lock (_gate)
            {
                return _builder.Length > 0;
            }
        }
    }

    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        lock (_gate)
        {
            _builder.Append(text);
        }
    }

    /// <summary>Returns everything collected so far and empties the buffer.</summary>
    public string TakeAll()
    {
        lock (_gate)
        {
            var text = _builder.ToString();
            _builder.Clear();
            return text;
        }
    }
}
