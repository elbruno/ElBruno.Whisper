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

    private static byte[] CreateRawPcm16Bytes(float[] samples)
    {
        var bytes = new byte[samples.Length * sizeof(short)];
        for (int i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1.0f, 1.0f);
            var value = (short)Math.Round(clamped * short.MaxValue);
            BitConverter.GetBytes(value).CopyTo(bytes, i * sizeof(short));
        }

        return bytes;
    }

    private static byte[] CreateRawFloat32Bytes(float[] samples)
    {
        var bytes = new byte[samples.Length * sizeof(float)];
        for (int i = 0; i < samples.Length; i++)
        {
            BitConverter.GetBytes(samples[i]).CopyTo(bytes, i * sizeof(float));
        }

        return bytes;
    }

    private static byte[] CreateWavBytes(float[] monoSamples, int sampleRate)
    {
        var pcmBytes = CreateRawPcm16Bytes(monoSamples);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmBytes.Length);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmBytes.Length);
        writer.Write(pcmBytes);
        writer.Flush();
        return stream.ToArray();
    }

    private static float[] CreateSineWave(int sampleRate, double durationSeconds, int channels = 1)
    {
        var sampleCountPerChannel = (int)(sampleRate * durationSeconds);
        var samples = new float[sampleCountPerChannel * channels];

        for (int frame = 0; frame < sampleCountPerChannel; frame++)
        {
            var sample = (float)Math.Sin((2 * Math.PI * 440 * frame) / sampleRate);
            for (int channel = 0; channel < channels; channel++)
            {
                samples[(frame * channels) + channel] = channel == 0 ? sample : sample * 0.5f;
            }
        }

        return samples;
    }

    [Theory]
    [InlineData("test-audio-failing.wav")]
    [InlineData("test-audio-small.wav")]
    [InlineData("test-audio-medium.wav")]
    public void ProcessAudioFile_ReturnsExpectedLength(string fileName)
    {
        var processor = new AudioProcessor();
        var path = GetTestDataPath(fileName);

        var (result, _) = processor.ProcessAudioFile(path);

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

        var (result, _) = processor.ProcessAudioFile(path);

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
        var (result, _) = processor.ProcessAudioStream(stream);

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

        var (fileResult, _) = processor.ProcessAudioFile(path);

        using var stream = File.OpenRead(path);
        var (streamResult, _) = processor.ProcessAudioStream(stream);

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

        var (result, _) = processor.ProcessAudioFile(path);

        Assert.Contains(result, value => value != 0.0f);
    }

    [Theory]
    [InlineData("test-audio-failing.wav")]
    [InlineData("test-audio-small.wav")]
    [InlineData("test-audio-medium.wav")]
    public void ProcessAudioFile_ReturnsPositiveAudioDuration(string fileName)
    {
        var processor = new AudioProcessor();
        var path = GetTestDataPath(fileName);

        var (_, audioDuration) = processor.ProcessAudioFile(path);

        Assert.True(audioDuration > TimeSpan.Zero, "Audio duration should be positive");
    }

    [Fact]
    public void ProcessAudioStream_WavMemoryStream_ReturnsExpectedLength_AndLeavesStreamOpen()
    {
        var processor = new AudioProcessor();
        var wavBytes = CreateWavBytes(CreateSineWave(AudioProcessor.TargetSampleRate, 0.25), AudioProcessor.TargetSampleRate);
        using var stream = new MemoryStream(wavBytes);

        var (result, duration) = processor.ProcessAudioStream(stream);

        Assert.Equal(ExpectedMelLength, result.Length);
        Assert.True(duration > TimeSpan.Zero);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void ProcessAudioStream_RawPcm16Mono_ReturnsExpectedLength()
    {
        var processor = new AudioProcessor();
        var samples = CreateSineWave(AudioProcessor.TargetSampleRate, 0.5);
        using var stream = new MemoryStream(CreateRawPcm16Bytes(samples));

        var (result, duration) = processor.ProcessAudioStream(
            stream,
            new WhisperAudioFormat(AudioProcessor.TargetSampleRate, 1, WhisperAudioSampleFormat.Pcm16));

        Assert.Equal(ExpectedMelLength, result.Length);
        Assert.InRange(duration, TimeSpan.FromMilliseconds(490), TimeSpan.FromMilliseconds(510));
    }

    [Fact]
    public void ProcessAudioBytes_RawFloat32Mono_ReturnsExpectedLength()
    {
        var processor = new AudioProcessor();
        var samples = CreateSineWave(AudioProcessor.TargetSampleRate, 0.5);

        var (result, duration) = processor.ProcessAudioBytes(
            CreateRawFloat32Bytes(samples),
            new WhisperAudioFormat(AudioProcessor.TargetSampleRate, 1, WhisperAudioSampleFormat.Float32));

        Assert.Equal(ExpectedMelLength, result.Length);
        Assert.InRange(duration, TimeSpan.FromMilliseconds(490), TimeSpan.FromMilliseconds(510));
    }

    [Fact]
    public void ReadAudioBytes_48KhzStereoNormalizesTo16KhzMono()
    {
        var processor = new AudioProcessor();
        var samples = CreateSineWave(48000, 1.0, channels: 2);

        var processedAudio = processor.ReadAudioBytes(
            CreateRawPcm16Bytes(samples),
            new WhisperAudioFormat(48000, 2, WhisperAudioSampleFormat.Pcm16));

        Assert.Equal(AudioProcessor.TargetSampleRate, processedAudio.Samples.Length);
        Assert.InRange(processedAudio.Duration, TimeSpan.FromMilliseconds(985), TimeSpan.FromMilliseconds(1015));
    }

    [Fact]
    public void ReadAudioBytes_ThrowsTypedErrorForMalformedInput()
    {
        var processor = new AudioProcessor();
        var malformedBytes = new byte[] { 0x00, 0x01, 0x02 };

        Assert.Throws<WhisperAudioFormatException>(() => processor.ReadAudioBytes(
            malformedBytes,
            new WhisperAudioFormat(AudioProcessor.TargetSampleRate, 1, WhisperAudioSampleFormat.Pcm16)));
    }

    [Fact]
    public void ProcessAudioBytes_HonorsCancellationDuringPreprocessing()
    {
        var processor = new AudioProcessor();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        Assert.Throws<OperationCanceledException>(() => processor.ProcessAudioBytes(
            CreateRawPcm16Bytes(CreateSineWave(AudioProcessor.TargetSampleRate, 1.0)),
            new WhisperAudioFormat(AudioProcessor.TargetSampleRate, 1, WhisperAudioSampleFormat.Pcm16),
            cancellationSource.Token));
    }
}
