using System.Runtime.InteropServices;
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

    public static WavReader FromFile(string path, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(path);
        return FromStream(stream, cancellationToken);
    }

    public static WavReader FromStream(Stream stream, CancellationToken cancellationToken = default)
    {
        var reader = new WavReader();
        reader.Read(stream, cancellationToken);
        return reader;
    }

    public static WavReader FromBytes(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array is not null)
        {
            using var stream = new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false, publiclyVisible: true);
            return FromStream(stream, cancellationToken);
        }

        using var copyStream = new MemoryStream(data.ToArray(), writable: false);
        return FromStream(copyStream, cancellationToken);
    }

    private void Read(Stream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // Read RIFF header
        var riff = br.ReadChars(4);
        if (new string(riff) != "RIFF")
            throw new WhisperAudioFormatException("Not a valid WAV file (missing RIFF header).");

        _ = br.ReadInt32();
        var wave = br.ReadChars(4);
        if (new string(wave) != "WAVE")
            throw new WhisperAudioFormatException("Not a valid WAV file (missing WAVE header).");

        var foundFormatChunk = false;

        // Find fmt chunk
        while (stream.Position < stream.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkId = br.ReadChars(4);
            var chunkSize = br.ReadInt32();
            var chunkName = new string(chunkId);

            if (chunkName == "fmt ")
            {
                if (chunkSize < 16)
                {
                    throw new WhisperAudioFormatException("Invalid WAV file (truncated fmt chunk).");
                }

                var audioFormat = br.ReadInt16(); // 1 = PCM
                Channels = br.ReadInt16();
                SampleRate = br.ReadInt32();
                _ = br.ReadInt32();
                _ = br.ReadInt16();
                BitsPerSample = br.ReadInt16();

                if (audioFormat != 1)
                    throw new WhisperAudioFormatException("Only PCM WAV files are supported.");
                if (BitsPerSample != 16)
                    throw new WhisperAudioFormatException("Only 16-bit PCM WAV files are supported.");

                // Skip any extra format bytes
                var extraSize = chunkSize - 16;
                if (extraSize > 0)
                    br.ReadBytes(extraSize);

                foundFormatChunk = true;
            }
            else if (chunkName == "data")
            {
                if (!foundFormatChunk)
                {
                    throw new WhisperAudioFormatException("Invalid WAV file (data chunk found before fmt chunk).");
                }

                // Read PCM data
                var sampleCount = chunkSize / (BitsPerSample / 8);
                var samples = new float[sampleCount];

                for (int i = 0; i < sampleCount; i++)
                {
                    if ((i & 0x3FF) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    var sample = br.ReadInt16();
                    // Normalize to [-1, 1]
                    samples[i] = sample / 32768.0f;
                }

                Samples = samples;
                break;
            }
            else
            {
                // Skip unknown chunk
                br.ReadBytes(chunkSize);
            }
        }

        if (Samples.Length == 0)
            throw new WhisperAudioFormatException("No audio data found in WAV file.");
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
