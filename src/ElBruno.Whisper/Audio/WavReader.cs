using System.Text;

namespace ElBruno.Whisper.Audio;

/// <summary>
/// Simple WAV file reader for 16-bit PCM audio.
/// </summary>
internal sealed class WavReader
{
    public int SampleRate { get; private set; }
    public int Channels { get; private set; }
    public int BitsPerSample { get; private set; }
    public float[] Samples { get; private set; } = [];

    public static WavReader FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return FromStream(stream);
    }

    public static WavReader FromStream(Stream stream)
    {
        var reader = new WavReader();
        reader.Read(stream);
        return reader;
    }

    private void Read(Stream stream)
    {
        using var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // Read RIFF header
        var riff = br.ReadChars(4);
        if (new string(riff) != "RIFF")
            throw new InvalidDataException("Not a valid WAV file (missing RIFF header)");

        var fileSize = br.ReadInt32();
        var wave = br.ReadChars(4);
        if (new string(wave) != "WAVE")
            throw new InvalidDataException("Not a valid WAV file (missing WAVE header)");

        // Find fmt chunk
        while (stream.Position < stream.Length)
        {
            var chunkId = br.ReadChars(4);
            var chunkSize = br.ReadInt32();
            var chunkName = new string(chunkId);

            if (chunkName == "fmt ")
            {
                var audioFormat = br.ReadInt16(); // 1 = PCM
                Channels = br.ReadInt16();
                SampleRate = br.ReadInt32();
                var byteRate = br.ReadInt32();
                var blockAlign = br.ReadInt16();
                BitsPerSample = br.ReadInt16();

                if (audioFormat != 1)
                    throw new NotSupportedException("Only PCM WAV files are supported");
                if (BitsPerSample != 16)
                    throw new NotSupportedException("Only 16-bit PCM is supported");

                // Skip any extra format bytes
                var extraSize = chunkSize - 16;
                if (extraSize > 0)
                    br.ReadBytes(extraSize);
            }
            else if (chunkName == "data")
            {
                // Read PCM data
                var sampleCount = chunkSize / (BitsPerSample / 8);
                var samples = new List<float>(sampleCount);

                for (int i = 0; i < sampleCount; i++)
                {
                    var sample = br.ReadInt16();
                    // Normalize to [-1, 1]
                    samples.Add(sample / 32768.0f);
                }

                Samples = samples.ToArray();
                break;
            }
            else
            {
                // Skip unknown chunk
                br.ReadBytes(chunkSize);
            }
        }

        if (Samples.Length == 0)
            throw new InvalidDataException("No audio data found in WAV file");
    }

    /// <summary>
    /// Convert stereo to mono by averaging channels.
    /// </summary>
    public void ConvertToMono()
    {
        if (Channels == 1)
            return;

        var monoSamples = new float[Samples.Length / Channels];
        for (int i = 0; i < monoSamples.Length; i++)
        {
            float sum = 0;
            for (int ch = 0; ch < Channels; ch++)
            {
                sum += Samples[i * Channels + ch];
            }
            monoSamples[i] = sum / Channels;
        }

        Samples = monoSamples;
        Channels = 1;
    }

    /// <summary>
    /// Resample audio to target sample rate using linear interpolation.
    /// </summary>
    public void Resample(int targetSampleRate)
    {
        if (SampleRate == targetSampleRate)
            return;

        var ratio = (double)SampleRate / targetSampleRate;
        var newLength = (int)(Samples.Length / ratio);
        var resampled = new float[newLength];

        for (int i = 0; i < newLength; i++)
        {
            var srcIndex = i * ratio;
            var srcIndexInt = (int)srcIndex;
            var frac = srcIndex - srcIndexInt;

            if (srcIndexInt + 1 < Samples.Length)
            {
                // Linear interpolation
                resampled[i] = (float)(Samples[srcIndexInt] * (1 - frac) + Samples[srcIndexInt + 1] * frac);
            }
            else
            {
                resampled[i] = Samples[srcIndexInt];
            }
        }

        Samples = resampled;
        SampleRate = targetSampleRate;
    }
}
