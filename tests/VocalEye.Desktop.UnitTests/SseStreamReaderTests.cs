using System.Text;
using FluentAssertions;
using VocalEye.Desktop.ApiClient;

namespace VocalEye.Desktop.UnitTests;

public class SseStreamReaderTests
{
    private static async Task<List<SseEvent>> ReadAll(string text, int chunk = 1000, CancellationToken ct = default)
    {
        var list = new List<SseEvent>();
        await foreach (var e in SseStreamReader.ReadEventsAsync(new ChunkedStream(Encoding.UTF8.GetBytes(text), chunk), ct))
            list.Add(e);
        return list;
    }

    [Fact]
    public async Task Parses_multiple_events_in_order()
    {
        var events = await ReadAll("event: text\ndata: {\"text\":\"a\"}\n\nevent: text\ndata: {\"text\":\"b\"}\n\n");
        events.Select(e => e.Data).Should().Equal("{\"text\":\"a\"}", "{\"text\":\"b\"}");
        events.Should().OnlyContain(e => e.EventName == "text");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Event_split_across_network_reads_is_reassembled(int chunk)
    {
        var events = await ReadAll("event: completed\ndata: {\"reply\":\"héllo\"}\n\n", chunk);
        events.Should().ContainSingle().Which.Data.Should().Be("{\"reply\":\"héllo\"}");
    }

    [Fact]
    public async Task Multiple_data_lines_are_joined_with_newline()
    {
        var events = await ReadAll("event: text\ndata: line1\ndata: line2\n\n");
        events.Should().ContainSingle().Which.Data.Should().Be("line1\nline2");
    }

    [Fact]
    public async Task Handles_crlf_comments_and_default_event_name()
    {
        var events = await ReadAll(": keep-alive\r\ndata: x\r\n\r\n");
        events.Should().ContainSingle().Which.Should().Be(new SseEvent("message", "x"));
    }

    [Fact]
    public async Task Unterminated_event_is_discarded()
    {
        var events = await ReadAll("event: text\ndata: partial");
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancellation_stops_reading()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => ReadAll("data: x\n\n", ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
