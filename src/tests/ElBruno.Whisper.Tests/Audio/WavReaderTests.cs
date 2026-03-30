using Xunit;
using ElBruno.Whisper.Audio;

namespace ElBruno.Whisper.Tests.Audio;

public class WavReaderTests
{
    private static byte[] CreateValidWavHeader(int sampleRate, short numChannels, int dataSize)
    {
        var header = new List<byte>();
        
        // RIFF header
        header.AddRange(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        header.AddRange(BitConverter.GetBytes(36 + dataSize)); // Chunk size
        header.AddRange(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        
        // fmt chunk
        header.AddRange(System.Text.Encoding.ASCII.GetBytes("fmt "));
        header.AddRange(BitConverter.GetBytes(16)); // Subchunk1 size
        header.AddRange(BitConverter.GetBytes((short)1)); // Audio format (PCM)
        header.AddRange(BitConverter.GetBytes(numChannels)); // Num channels
        header.AddRange(BitConverter.GetBytes(sampleRate)); // Sample rate
        header.AddRange(BitConverter.GetBytes(sampleRate * numChannels * 2)); // Byte rate
        header.AddRange(BitConverter.GetBytes((short)(numChannels * 2))); // Block align
        header.AddRange(BitConverter.GetBytes((short)16)); // Bits per sample
        
        // data chunk
        header.AddRange(System.Text.Encoding.ASCII.GetBytes("data"));
        header.AddRange(BitConverter.GetBytes(dataSize)); // Subchunk2 size
        
        return header.ToArray();
    }

    [Fact]
    public void CanParseValidWavHeader()
    {
        var dataSize = 1600; // 100 samples * 2 bytes * 1 channel
        var header = CreateValidWavHeader(16000, 1, dataSize);
        var data = new byte[dataSize];
        var wavBytes = header.Concat(data).ToArray();

        using var stream = new MemoryStream(wavBytes);
        var wavData = WavReader.FromStream(stream);

        Assert.NotNull(wavData);
        Assert.Equal(16000, wavData.SampleRate);
        Assert.Equal(1, wavData.Channels);
    }

    [Fact]
    public void ThrowsOnInvalidWavFile()
    {
        var invalidData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        using var stream = new MemoryStream(invalidData);
        
        Assert.Throws<InvalidDataException>(() => WavReader.FromStream(stream));
    }

    [Fact]
    public void Reads16BitPcmDataCorrectly()
    {
        var dataSize = 4; // 2 samples * 2 bytes
        var header = CreateValidWavHeader(16000, 1, dataSize);
        var data = new byte[] { 0x00, 0x10, 0x00, 0x20 }; // Two 16-bit samples
        var wavBytes = header.Concat(data).ToArray();

        using var stream = new MemoryStream(wavBytes);
        var wavData = WavReader.FromStream(stream);

        Assert.NotNull(wavData.Samples);
        Assert.Equal(2, wavData.Samples.Length);
    }

    [Fact]
    public void HandlesMonoAudio()
    {
        var dataSize = 1600;
        var header = CreateValidWavHeader(16000, 1, dataSize);
        var data = new byte[dataSize];
        var wavBytes = header.Concat(data).ToArray();

        using var stream = new MemoryStream(wavBytes);
        var wavData = WavReader.FromStream(stream);

        Assert.Equal(1, wavData.Channels);
    }

    [Fact]
    public void HandlesStereoAudio()
    {
        var dataSize = 3200; // 100 samples * 2 bytes * 2 channels
        var header = CreateValidWavHeader(16000, 2, dataSize);
        var data = new byte[dataSize];
        var wavBytes = header.Concat(data).ToArray();

        using var stream = new MemoryStream(wavBytes);
        var wavData = WavReader.FromStream(stream);

        Assert.Equal(2, wavData.Channels);
    }

    [Fact]
    public void ReportsCorrectSampleRate()
    {
        var sampleRate = 44100;
        var dataSize = 4410; // 100ms of audio
        var header = CreateValidWavHeader(sampleRate, 1, dataSize);
        var data = new byte[dataSize];
        var wavBytes = header.Concat(data).ToArray();

        using var stream = new MemoryStream(wavBytes);
        var wavData = WavReader.FromStream(stream);

        Assert.Equal(44100, wavData.SampleRate);
    }

    [Fact]
    public void ThrowsOnTooSmallFile()
    {
        var tooSmall = new byte[10];

        using var stream = new MemoryStream(tooSmall);
        
        Assert.Throws<InvalidDataException>(() => WavReader.FromStream(stream));
    }

    [Fact]
    public void ThrowsOnInvalidRiffHeader()
    {
        var invalidRiff = System.Text.Encoding.ASCII.GetBytes("XXXX");
        var rest = new byte[40];
        var wavBytes = invalidRiff.Concat(rest).ToArray();

        using var stream = new MemoryStream(wavBytes);
        
        Assert.Throws<InvalidDataException>(() => WavReader.FromStream(stream));
    }

    [Fact]
    public void CanReadFromFileStream()
    {
        var dataSize = 1600;
        var header = CreateValidWavHeader(16000, 1, dataSize);
        var data = new byte[dataSize];
        var wavBytes = header.Concat(data).ToArray();

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, wavBytes);

            using var fileStream = File.OpenRead(tempFile);
            var wavData = WavReader.FromStream(fileStream);

            Assert.NotNull(wavData);
            Assert.Equal(16000, wavData.SampleRate);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
