using ElBruno.Whisper.Audio;
using Xunit;

namespace ElBruno.Whisper.Tests.Audio;

public class AudioProcessorTests
{
    private const int ExpectedMelLength = 80 * 3000; // [1, 80, 3000] flattened

    private static string GetTestDataPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
    }

    [Theory]
    [InlineData("test-audio-failing.wav")]
    [InlineData("test-audio-small.wav")]
    [InlineData("test-audio-medium.wav")]
    public void ProcessAudioFile_ReturnsExpectedLength(string fileName)
    {
        var processor = new AudioProcessor();
        var path = GetTestDataPath(fileName);

        var result = processor.ProcessAudioFile(path);

        Assert.NotNull(result);
        Assert.Equal(ExpectedMelLength, result.Length);
    }

    [Theory]
    [InlineData("test-audio-failing.wav")]
    [InlineData("test-audio-small.wav")]
    [InlineData("test-audio-medium.wav")]
    public void ProcessAudioFile_ReturnsFiniteValues(string fileName)
    {
        var processor = new AudioProcessor();
        var path = GetTestDataPath(fileName);

        var result = processor.ProcessAudioFile(path);

        Assert.All(result, value =>
        {
            Assert.False(float.IsNaN(value), "Mel spectrogram should not contain NaN values");
            Assert.False(float.IsInfinity(value), "Mel spectrogram should not contain Infinity values");
        });
    }

    [Theory]
    [InlineData("test-audio-failing.wav")]
    [InlineData("test-audio-small.wav")]
    [InlineData("test-audio-medium.wav")]
    public void ProcessAudioStream_ReturnsExpectedLength(string fileName)
    {
        var processor = new AudioProcessor();
        var path = GetTestDataPath(fileName);

        using var stream = File.OpenRead(path);
        var result = processor.ProcessAudioStream(stream);

        Assert.NotNull(result);
        Assert.Equal(ExpectedMelLength, result.Length);
    }

    [Theory]
    [InlineData("test-audio-failing.wav")]
    [InlineData("test-audio-small.wav")]
    [InlineData("test-audio-medium.wav")]
    public void ProcessAudioStream_MatchesProcessAudioFile(string fileName)
    {
        var processor = new AudioProcessor();
        var path = GetTestDataPath(fileName);

        var fileResult = processor.ProcessAudioFile(path);

        using var stream = File.OpenRead(path);
        var streamResult = processor.ProcessAudioStream(stream);

        Assert.Equal(fileResult.Length, streamResult.Length);
        for (int i = 0; i < fileResult.Length; i++)
        {
            Assert.Equal(fileResult[i], streamResult[i], precision: 5);
        }
    }

    [Fact]
    public void ProcessAudioFile_ThrowsForNonExistentFile()
    {
        var processor = new AudioProcessor();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.wav");

        Assert.ThrowsAny<Exception>(() => processor.ProcessAudioFile(nonExistentPath));
    }

    [Fact]
    public void ProcessAudioStream_ThrowsForEmptyStream()
    {
        var processor = new AudioProcessor();

        using var emptyStream = new MemoryStream();

        Assert.ThrowsAny<Exception>(() => processor.ProcessAudioStream(emptyStream));
    }

    [Fact]
    public void ProcessAudioStream_ThrowsForInvalidData()
    {
        var processor = new AudioProcessor();
        var invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };

        using var stream = new MemoryStream(invalidData);

        Assert.ThrowsAny<Exception>(() => processor.ProcessAudioStream(stream));
    }

    [Fact]
    public void Constructor_CreatesInstance()
    {
        var processor = new AudioProcessor();

        Assert.NotNull(processor);
    }

    [Theory]
    [InlineData("test-audio-failing.wav")]
    [InlineData("test-audio-small.wav")]
    [InlineData("test-audio-medium.wav")]
    public void ProcessAudioFile_ContainsNonZeroValues(string fileName)
    {
        var processor = new AudioProcessor();
        var path = GetTestDataPath(fileName);

        var result = processor.ProcessAudioFile(path);

        Assert.Contains(result, value => value != 0.0f);
    }
}
