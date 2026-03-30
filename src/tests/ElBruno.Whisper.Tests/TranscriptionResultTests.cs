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
}
