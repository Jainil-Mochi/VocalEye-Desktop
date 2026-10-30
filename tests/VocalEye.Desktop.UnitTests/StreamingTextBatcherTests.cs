using FluentAssertions;
using VocalEye.Desktop.Core.Streaming;

namespace VocalEye.Desktop.UnitTests;

public class StreamingTextBatcherTests
{
    [Fact]
    public async Task Dispose_flushes_remaining_text_in_order()
    {
        var received = new List<string>();
        var batcher = new StreamingTextBatcher(t => { lock (received) received.Add(t); }, TimeSpan.FromMilliseconds(20));
        foreach (var c in new[] { "a", "b", "c", "d" }) batcher.Append(c);
        await batcher.DisposeAsync();
        string.Concat(received).Should().Be("abcd");
    }

    [Fact]
    public async Task Many_tiny_chunks_are_batched_into_fewer_flushes()
    {
        var flushes = 0;
        var batcher = new StreamingTextBatcher(_ => Interlocked.Increment(ref flushes), TimeSpan.FromMilliseconds(100));
        for (var i = 0; i < 500; i++) batcher.Append("x");
        await batcher.DisposeAsync();
        flushes.Should().BeLessThan(10);
    }
}
