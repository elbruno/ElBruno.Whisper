using Xunit;

namespace ElBruno.Whisper.Tests;

public class TranscriptionWordTests
{
    [Fact]
    public void TranscriptionWord_HasStartEndAndText()
    {
        var word = new TranscriptionWord
        {
            Start = TimeSpan.FromSeconds(0.25),
            End = TimeSpan.FromSeconds(0.75),
            Text = "Hello"
        };

        Assert.Equal(TimeSpan.FromSeconds(0.25), word.Start);
        Assert.Equal(TimeSpan.FromSeconds(0.75), word.End);
        Assert.Equal("Hello", word.Text);
    }

    [Fact]
    public void TranscriptionWord_IsRecord_SupportsEquality()
    {
        var left = new TranscriptionWord
        {
            Start = TimeSpan.FromSeconds(1),
            End = TimeSpan.FromSeconds(2),
            Text = "world"
        };

        var right = new TranscriptionWord
        {
            Start = TimeSpan.FromSeconds(1),
            End = TimeSpan.FromSeconds(2),
            Text = "world"
        };

        Assert.Equal(left, right);
    }
}
