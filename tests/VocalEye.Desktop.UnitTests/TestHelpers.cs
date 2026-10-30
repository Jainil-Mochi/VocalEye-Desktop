using System.Net;
using System.Text;

namespace VocalEye.Desktop.UnitTests;

internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string? Body)> Calls { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        Calls.Add((request, body));
        return await responder(request, ct);
    }
}

/// <summary>Stream that returns at most <c>chunkSize</c> bytes per read, to simulate arbitrary network chunking.</summary>
internal sealed class ChunkedStream(byte[] data, int chunkSize) : Stream
{
    private int _pos;
    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = Math.Min(Math.Min(count, chunkSize), data.Length - _pos);
        Array.Copy(data, _pos, buffer, offset, n);
        _pos += n;
        return n;
    }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _pos; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal static class Http
{
    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Sse(string body, int chunkSize = 7) =>
        new(HttpStatusCode.OK)
        {
            Content = new StreamContent(new ChunkedStream(Encoding.UTF8.GetBytes(body), chunkSize))
            { Headers = { ContentType = new("text/event-stream") } }
        };
}
