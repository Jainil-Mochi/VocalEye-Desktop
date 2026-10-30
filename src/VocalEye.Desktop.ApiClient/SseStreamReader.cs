using System.Runtime.CompilerServices;
using System.Text;

namespace VocalEye.Desktop.ApiClient;

/// <summary>One parsed server-sent event. <see cref="EventName"/> defaults to "message".</summary>
public sealed record SseEvent(string EventName, string Data);

/// <summary>
/// Incremental Server-Sent Events parser. Events are delimited by blank lines (not by network read
/// boundaries); multiple <c>data:</c> lines are joined with '\n'; comment lines (":") are ignored.
/// </summary>
public static class SseStreamReader
{
    public static async IAsyncEnumerable<SseEvent> ReadEventsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024, leaveOpen: true);

        string? eventName = null;
        var data = new StringBuilder();
        var hasData = false;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null) yield break; // end of stream; an unterminated event is discarded per the SSE spec

            if (line.Length == 0)
            {
                if (hasData)
                    yield return new SseEvent(eventName ?? "message", data.ToString());
                eventName = null;
                data.Clear();
                hasData = false;
                continue;
            }

            if (line[0] == ':') continue; // comment / keep-alive

            string field, value;
            var colon = line.IndexOf(':');
            if (colon < 0) { field = line; value = string.Empty; }
            else
            {
                field = line[..colon];
                value = line[(colon + 1)..];
                if (value.StartsWith(' ')) value = value[1..];
            }

            switch (field)
            {
                case "event":
                    eventName = value;
                    break;
                case "data":
                    if (hasData) data.Append('\n');
                    data.Append(value);
                    hasData = true;
                    break;
                // "id" and "retry" are not needed by this client.
            }
        }
    }
}
