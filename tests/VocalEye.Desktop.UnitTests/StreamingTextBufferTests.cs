using FluentAssertions;
using VocalEye.Desktop.Core.Services;

namespace VocalEye.Desktop.UnitTests;

public class StreamingTextBufferTests
{
    [Fact]
    public void TakeAll_ReturnsAppendedTextInOrder_AndEmptiesBuffer()
    {
        var buffer = new StreamingTextBuffer();
        buffer.Append("Hello ");
        buffer.Append("world");

        var text = buffer.TakeAll();

        text.Should().Be("Hello world");
        buffer.HasPending.Should().BeFalse();
        buffer.TakeAll().Should().BeEmpty();
    }

    [Fact]
    public void Append_IgnoresNullOrEmptyText()
    {
        var buffer = new StreamingTextBuffer();

        buffer.Append(string.Empty);
        buffer.Append(null!);

        buffer.HasPending.Should().BeFalse();
    }
}
