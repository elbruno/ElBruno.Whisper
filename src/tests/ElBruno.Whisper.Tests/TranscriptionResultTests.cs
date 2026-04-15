using Xunit;

namespace ElBruno.Whisper.Tests;

public class TranscriptionResultTests
{
    [Fact]
    public void TranscriptionResult_HasTextProperty()
    {
        var result = new TranscriptionResult
        {
            Text = "Hello, world!"
        };

        Assert.Equal("Hello, world!", result.Text);
    }

    [Fact]
    public void CanSetDetectedLanguage()
    {
        var result = new TranscriptionResult
        {
            Text = "Hello, world!",
            DetectedLanguage = "en"
        };

        Assert.Equal("en", result.DetectedLanguage);
    }

    [Fact]
    public void CanSetDuration()
    {
        var duration = TimeSpan.FromSeconds(30);
        var result = new TranscriptionResult
        {
            Text = "Hello, world!",
            Duration = duration
        };

        Assert.Equal(duration, result.Duration);
    }

    [Fact]
    public void TranscriptionResult_CanHaveEmptyText()
    {
        var result = new TranscriptionResult
        {
            Text = string.Empty
        };

        Assert.Equal(string.Empty, result.Text);
    }

    [Fact]
    public void TranscriptionResult_CanHaveNullDetectedLanguage()
    {
        var result = new TranscriptionResult
        {
            Text = "Hello, world!",
            DetectedLanguage = null
        };

        Assert.Null(result.DetectedLanguage);
    }

    [Fact]
    public void TranscriptionResult_CanHaveDefaultDuration()
    {
        var result = new TranscriptionResult
        {
            Text = "Hello, world!"
        };

        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    [Fact]
    public void TranscriptionResult_SupportsLongText()
    {
        var longText = new string('a', 10000);
        var result = new TranscriptionResult
        {
            Text = longText
        };

        Assert.Equal(10000, result.Text.Length);
    }

    [Fact]
    public void TranscriptionResult_SupportsMultilineText()
    {
        var multilineText = "Line 1\nLine 2\nLine 3";
        var result = new TranscriptionResult
        {
            Text = multilineText
        };

        Assert.Contains("\n", result.Text);
        Assert.Equal(multilineText, result.Text);
    }

    [Fact]
    public void Duration_CanBeZero()
    {
        var result = new TranscriptionResult
        {
            Text = "Hello",
            Duration = TimeSpan.Zero
        };

        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    [Fact]
    public void Duration_CanBeVeryLong()
    {
        var longDuration = TimeSpan.FromHours(2);
        var result = new TranscriptionResult
        {
            Text = "Very long audio",
            Duration = longDuration
        };

        Assert.Equal(longDuration, result.Duration);
    }

    [Fact]
    public void DefaultSegments_ShouldBeNull()
    {
        var result = new TranscriptionResult
        {
            Text = "Hello"
        };

        Assert.Null(result.Segments);
    }

    [Fact]
    public void CanSetSegments()
    {
        var segments = new List<TranscriptionSegment>
        {
            new()
            {
                Start = TimeSpan.FromSeconds(0),
                End = TimeSpan.FromSeconds(1),
                Text = "Hello"
            },
            new()
            {
                Start = TimeSpan.FromSeconds(1),
                End = TimeSpan.FromSeconds(2),
                Text = "world"
            }
        };

        var result = new TranscriptionResult
        {
            Text = "Hello world",
            Segments = segments
        };

        Assert.NotNull(result.Segments);
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal("Hello", result.Segments[0].Text);
        Assert.Equal("world", result.Segments[1].Text);
    }

    [Fact]
    public void Segments_CanBeEmptyList()
    {
        var result = new TranscriptionResult
        {
            Text = "Hello",
            Segments = new List<TranscriptionSegment>()
        };

        Assert.NotNull(result.Segments);
        Assert.Empty(result.Segments);
    }

    [Fact]
    public void Segments_EmptyListIsNotNull()
    {
        var result = new TranscriptionResult
        {
            Text = "Hello",
            Segments = new List<TranscriptionSegment>()
        };

        Assert.NotNull(result.Segments);
    }

    [Fact]
    public void Segments_IsReadOnlyList()
    {
        var segments = new List<TranscriptionSegment>
        {
            new()
            {
                Start = TimeSpan.Zero,
                End = TimeSpan.FromSeconds(1),
                Text = "Test"
            }
        };

        var result = new TranscriptionResult
        {
            Text = "Test",
            Segments = segments
        };

        Assert.IsAssignableFrom<IReadOnlyList<TranscriptionSegment>>(result.Segments);
    }

    [Fact]
    public void Segments_WithTimestampData_HasCorrectTimes()
    {
        var segments = new List<TranscriptionSegment>
        {
            new()
            {
                Start = TimeSpan.FromSeconds(0.0),
                End = TimeSpan.FromSeconds(2.5),
                Text = "First segment"
            },
            new()
            {
                Start = TimeSpan.FromSeconds(2.5),
                End = TimeSpan.FromSeconds(5.0),
                Text = "Second segment"
            }
        };

        var result = new TranscriptionResult
        {
            Text = "First segment Second segment",
            Duration = TimeSpan.FromSeconds(5.0),
            Segments = segments
        };

        Assert.Equal(TimeSpan.FromSeconds(0.0), result.Segments![0].Start);
        Assert.Equal(TimeSpan.FromSeconds(2.5), result.Segments[0].End);
        Assert.Equal(TimeSpan.FromSeconds(2.5), result.Segments[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(5.0), result.Segments[1].End);
    }
}
