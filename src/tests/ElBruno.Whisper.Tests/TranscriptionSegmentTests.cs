using Xunit;

namespace ElBruno.Whisper.Tests;

public class TranscriptionSegmentTests
{
    [Fact]
    public void TranscriptionSegment_HasStartProperty()
    {
        var segment = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(1.5),
            End = TimeSpan.FromSeconds(3.0),
            Text = "Hello"
        };

        Assert.Equal(TimeSpan.FromSeconds(1.5), segment.Start);
    }

    [Fact]
    public void TranscriptionSegment_HasEndProperty()
    {
        var segment = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(0),
            End = TimeSpan.FromSeconds(2.5),
            Text = "Hello"
        };

        Assert.Equal(TimeSpan.FromSeconds(2.5), segment.End);
    }

    [Fact]
    public void TranscriptionSegment_HasTextProperty()
    {
        var segment = new TranscriptionSegment
        {
            Start = TimeSpan.Zero,
            End = TimeSpan.FromSeconds(1),
            Text = "test text"
        };

        Assert.Equal("test text", segment.Text);
    }

    [Fact]
    public void TranscriptionSegment_WithZeroTimeSpans()
    {
        var segment = new TranscriptionSegment
        {
            Start = TimeSpan.Zero,
            End = TimeSpan.Zero,
            Text = "instant"
        };

        Assert.Equal(TimeSpan.Zero, segment.Start);
        Assert.Equal(TimeSpan.Zero, segment.End);
    }

    [Fact]
    public void TranscriptionSegment_WithLargeTimeSpans()
    {
        var start = TimeSpan.FromHours(1);
        var end = TimeSpan.FromHours(1.5);
        var segment = new TranscriptionSegment
        {
            Start = start,
            End = end,
            Text = "long audio"
        };

        Assert.Equal(start, segment.Start);
        Assert.Equal(end, segment.End);
    }

    [Fact]
    public void TranscriptionSegment_WithMillisecondPrecision()
    {
        var start = TimeSpan.FromMilliseconds(1234);
        var end = TimeSpan.FromMilliseconds(5678);
        var segment = new TranscriptionSegment
        {
            Start = start,
            End = end,
            Text = "precise"
        };

        Assert.Equal(1234, segment.Start.TotalMilliseconds);
        Assert.Equal(5678, segment.End.TotalMilliseconds);
    }

    [Fact]
    public void TranscriptionSegment_IsRecord_SupportsEquality()
    {
        var segment1 = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(1),
            End = TimeSpan.FromSeconds(2),
            Text = "Hello"
        };

        var segment2 = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(1),
            End = TimeSpan.FromSeconds(2),
            Text = "Hello"
        };

        Assert.Equal(segment1, segment2);
    }

    [Fact]
    public void TranscriptionSegment_IsRecord_DifferentValues_NotEqual()
    {
        var segment1 = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(1),
            End = TimeSpan.FromSeconds(2),
            Text = "Hello"
        };

        var segment2 = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(1),
            End = TimeSpan.FromSeconds(3),
            Text = "Hello"
        };

        Assert.NotEqual(segment1, segment2);
    }

    [Fact]
    public void TranscriptionSegment_IsRecord_DifferentText_NotEqual()
    {
        var segment1 = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(0),
            End = TimeSpan.FromSeconds(1),
            Text = "Hello"
        };

        var segment2 = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(0),
            End = TimeSpan.FromSeconds(1),
            Text = "World"
        };

        Assert.NotEqual(segment1, segment2);
    }

    [Fact]
    public void TranscriptionSegment_IsRecord_SupportsHashCode()
    {
        var segment1 = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(1),
            End = TimeSpan.FromSeconds(2),
            Text = "Hello"
        };

        var segment2 = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(1),
            End = TimeSpan.FromSeconds(2),
            Text = "Hello"
        };

        Assert.Equal(segment1.GetHashCode(), segment2.GetHashCode());
    }

    [Fact]
    public void TranscriptionSegment_IsRecord_SupportsToString()
    {
        var segment = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(1),
            End = TimeSpan.FromSeconds(2),
            Text = "Hello"
        };

        var str = segment.ToString();

        Assert.Contains("Hello", str);
        Assert.Contains("Start", str);
        Assert.Contains("End", str);
    }

    [Fact]
    public void TranscriptionSegment_IsSealed()
    {
        Assert.True(typeof(TranscriptionSegment).IsSealed);
    }

    [Fact]
    public void TranscriptionSegment_IsRecord_CanUseWith()
    {
        var original = new TranscriptionSegment
        {
            Start = TimeSpan.FromSeconds(1),
            End = TimeSpan.FromSeconds(2),
            Text = "Hello"
        };

        var modified = original with { Text = "World" };

        Assert.Equal("World", modified.Text);
        Assert.Equal(original.Start, modified.Start);
        Assert.Equal(original.End, modified.End);
    }

    [Fact]
    public void TranscriptionSegment_EmptyText()
    {
        var segment = new TranscriptionSegment
        {
            Start = TimeSpan.Zero,
            End = TimeSpan.FromSeconds(1),
            Text = string.Empty
        };

        Assert.Equal(string.Empty, segment.Text);
    }
}
