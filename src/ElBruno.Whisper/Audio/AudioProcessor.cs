using System.Buffers;
using System.Buffers.Binary;

namespace ElBruno.Whisper.Audio;

/// <summary>
/// Processes audio files into log-mel spectrograms for Whisper inference.
/// </summary>
internal sealed class AudioProcessor
{
    internal const int TargetSampleRate = 16000;
    private const int MelBins = 80;
    private const int FftSize = 400; // 25ms at 16kHz
    private const int HopLength = 160; // 10ms at 16kHz
    private const int MaxFrames = 3000; // 30 seconds
    private const int MaxSamples = TargetSampleRate * 30; // 480,000 samples = 30 seconds
    private const int StreamBufferSize = 81920;

    private readonly MelSpectrogramProcessor _melProcessor;

    public AudioProcessor()
    {
        _melProcessor = new MelSpectrogramProcessor(
            sampleRate: TargetSampleRate,
            nMels: MelBins,
            nFft: FftSize,
            hopLength: HopLength
        );
    }

    /// <summary>
    /// Process a WAV file into a log-mel spectrogram tensor [1, 80, 3000].
    /// </summary>
    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessAudioFile(
        string path,
        CancellationToken cancellationToken = default)
    {
        var processedAudio = ReadAudioFile(path, cancellationToken);
        return ProcessNormalizedSamples(processedAudio.Samples, processedAudio.Duration, cancellationToken);
    }

    /// <summary>
    /// Process a WAV stream into a log-mel spectrogram tensor [1, 80, 3000].
    /// </summary>
    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessAudioStream(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var processedAudio = ReadAudioStream(stream, cancellationToken);
        return ProcessNormalizedSamples(processedAudio.Samples, processedAudio.Duration, cancellationToken);
    }

    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessAudioStream(
        Stream stream,
        WhisperAudioFormat format,
        CancellationToken cancellationToken = default)
    {
        var processedAudio = ReadAudioStream(stream, format, cancellationToken);
        return ProcessNormalizedSamples(processedAudio.Samples, processedAudio.Duration, cancellationToken);
    }

    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessAudioBytes(
        ReadOnlyMemory<byte> audioData,
        WhisperAudioFormat format,
        CancellationToken cancellationToken = default)
    {
        var processedAudio = ReadAudioBytes(audioData, format, cancellationToken);
        return ProcessNormalizedSamples(processedAudio.Samples, processedAudio.Duration, cancellationToken);
    }

    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessAudioSamples(
        ReadOnlyMemory<float> audioData,
        WhisperAudioFormat format,
        CancellationToken cancellationToken = default)
    {
        var processedAudio = ReadAudioSamples(audioData, format, cancellationToken);
        return ProcessNormalizedSamples(processedAudio.Samples, processedAudio.Duration, cancellationToken);
    }

    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessAudioSamples(
        ReadOnlyMemory<float> monoAudio,
        int sampleRate,
        CancellationToken cancellationToken = default)
    {
        var processedAudio = ReadAudioSamples(
            monoAudio,
            new WhisperAudioFormat(sampleRate, channels: 1, WhisperAudioSampleFormat.Float32),
            cancellationToken);

        return ProcessNormalizedSamples(processedAudio.Samples, processedAudio.Duration, cancellationToken);
    }

    public ProcessedAudio ReadAudioFile(string path, CancellationToken cancellationToken = default)
    {
        var wav = WavReader.FromFile(path, cancellationToken);
        return ReadAudio(wav, cancellationToken);
    }

    public ProcessedAudio ReadAudioStream(Stream stream, CancellationToken cancellationToken = default)
    {
        return ReadAudioPayload(ReadAllBytes(stream, cancellationToken), format: null, cancellationToken);
    }

    public ProcessedAudio ReadAudioStream(
        Stream stream,
        WhisperAudioFormat format,
        CancellationToken cancellationToken = default)
    {
        return ReadAudioPayload(ReadAllBytes(stream, cancellationToken), format, cancellationToken);
    }

    public ProcessedAudio ReadAudioBytes(
        ReadOnlyMemory<byte> audioData,
        WhisperAudioFormat format,
        CancellationToken cancellationToken = default)
    {
        return ReadAudioPayload(audioData, format: format, cancellationToken);
    }

    public ProcessedAudio ReadAudioSamples(
        ReadOnlyMemory<float> audioData,
        WhisperAudioFormat format,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (audioData.IsEmpty)
        {
            throw new WhisperAudioFormatException("Audio payload is empty.");
        }

        if (format.SampleFormat != WhisperAudioSampleFormat.Float32)
        {
            throw new WhisperAudioFormatException("Float sample buffers require WhisperAudioSampleFormat.Float32.");
        }

        return NormalizeAudio(audioData.ToArray(), format.SampleRate, format.Channels, cancellationToken);
    }

    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessNormalizedSamples(float[] samples)
    {
        var audioDuration = TimeSpan.FromSeconds((double)samples.Length / TargetSampleRate);
        return ProcessNormalizedSamples(samples, audioDuration, CancellationToken.None);
    }

    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessNormalizedSamples(
        float[] samples,
        TimeSpan audioDuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var paddedAudio = PadOrTrimAudio(samples, MaxSamples);
        cancellationToken.ThrowIfCancellationRequested();
        var melSpec = _melProcessor.ComputeMelSpectrogram(paddedAudio);
        cancellationToken.ThrowIfCancellationRequested();
        var result = PadOrTruncate(melSpec, MaxFrames);

        return (result, audioDuration);
    }

    private static ProcessedAudio ReadAudio(WavReader wav, CancellationToken cancellationToken)
    {
        return NormalizeAudio(wav.Samples, wav.SampleRate, wav.Channels, cancellationToken);
    }

    private static float[] PadOrTrimAudio(float[] samples, int targetLength)
    {
        if (samples.Length == targetLength)
            return samples;

        var result = new float[targetLength];
        var copyLength = Math.Min(samples.Length, targetLength);
        Array.Copy(samples, result, copyLength);
        // Remaining elements are already 0.0f (silence)
        return result;
    }

    private static ProcessedAudio ReadAudioPayload(
        ReadOnlyMemory<byte> audioData,
        WhisperAudioFormat? format,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (audioData.IsEmpty)
        {
            throw new WhisperAudioFormatException("Audio payload is empty.");
        }

        if (LooksLikeWav(audioData.Span))
        {
            var wav = WavReader.FromBytes(audioData, cancellationToken);
            return ReadAudio(wav, cancellationToken);
        }

        if (format is null)
        {
            throw new WhisperAudioFormatException(
                "Audio input is not a WAV container. Provide an explicit WhisperAudioFormat for raw PCM audio.");
        }

        return format.Value.SampleFormat switch
        {
            WhisperAudioSampleFormat.Pcm16 => NormalizeAudio(
                DecodePcm16(audioData.Span, format.Value, cancellationToken),
                format.Value.SampleRate,
                format.Value.Channels,
                cancellationToken),
            WhisperAudioSampleFormat.Float32 => NormalizeAudio(
                DecodeFloat32(audioData.Span, format.Value, cancellationToken),
                format.Value.SampleRate,
                format.Value.Channels,
                cancellationToken),
            _ => throw new WhisperAudioFormatException("Unsupported raw audio sample format.")
        };
    }

    private static ProcessedAudio NormalizeAudio(
        float[] samples,
        int sampleRate,
        int channels,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (samples.Length == 0)
        {
            throw new WhisperAudioFormatException("Audio payload does not contain any samples.");
        }

        var monoSamples = channels == 1
            ? samples
            : ConvertToMono(samples, channels, cancellationToken);
        var normalizedSamples = sampleRate == TargetSampleRate
            ? monoSamples
            : Resample(monoSamples, sampleRate, TargetSampleRate, cancellationToken);
        var duration = TimeSpan.FromSeconds((double)normalizedSamples.Length / TargetSampleRate);

        return new ProcessedAudio(normalizedSamples, duration);
    }

    private static float[] ConvertToMono(float[] samples, int channels, CancellationToken cancellationToken)
    {
        if (samples.Length % channels != 0)
        {
            throw new WhisperAudioFormatException("Audio payload length is not aligned to the provided channel count.");
        }

        var monoSamples = new float[samples.Length / channels];
        for (int i = 0; i < monoSamples.Length; i++)
        {
            if ((i & 0x3FF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            float sum = 0;
            for (int channel = 0; channel < channels; channel++)
            {
                sum += samples[(i * channels) + channel];
            }

            monoSamples[i] = sum / channels;
        }

        return monoSamples;
    }

    private static float[] Resample(
        float[] samples,
        int sourceSampleRate,
        int targetSampleRate,
        CancellationToken cancellationToken)
    {
        var ratio = (double)sourceSampleRate / targetSampleRate;
        var newLength = Math.Max(1, (int)(samples.Length / ratio));
        var resampled = new float[newLength];

        for (int i = 0; i < newLength; i++)
        {
            if ((i & 0x3FF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var srcIndex = i * ratio;
            var srcIndexInt = (int)srcIndex;
            var frac = srcIndex - srcIndexInt;

            if (srcIndexInt + 1 < samples.Length)
            {
                resampled[i] = (float)(samples[srcIndexInt] * (1 - frac) + samples[srcIndexInt + 1] * frac);
            }
            else
            {
                resampled[i] = samples[srcIndexInt];
            }
        }

        return resampled;
    }

    private static float[] DecodePcm16(
        ReadOnlySpan<byte> audioData,
        WhisperAudioFormat format,
        CancellationToken cancellationToken)
    {
        if (audioData.Length % format.BytesPerFrame != 0)
        {
            throw new WhisperAudioFormatException("PCM16 payload length is not aligned with the provided WhisperAudioFormat.");
        }

        var sampleCount = audioData.Length / format.BytesPerSample;
        var samples = new float[sampleCount];

        for (int sampleIndex = 0, byteOffset = 0; sampleIndex < sampleCount; sampleIndex++, byteOffset += sizeof(short))
        {
            if ((sampleIndex & 0x3FF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var sample = BinaryPrimitives.ReadInt16LittleEndian(audioData.Slice(byteOffset, sizeof(short)));
            samples[sampleIndex] = sample / 32768.0f;
        }

        return samples;
    }

    private static float[] DecodeFloat32(
        ReadOnlySpan<byte> audioData,
        WhisperAudioFormat format,
        CancellationToken cancellationToken)
    {
        if (audioData.Length % format.BytesPerFrame != 0)
        {
            throw new WhisperAudioFormatException("Float32 payload length is not aligned with the provided WhisperAudioFormat.");
        }

        var sampleCount = audioData.Length / format.BytesPerSample;
        var samples = new float[sampleCount];

        for (int sampleIndex = 0, byteOffset = 0; sampleIndex < sampleCount; sampleIndex++, byteOffset += sizeof(float))
        {
            if ((sampleIndex & 0x3FF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var bits = BinaryPrimitives.ReadInt32LittleEndian(audioData.Slice(byteOffset, sizeof(float)));
            samples[sampleIndex] = BitConverter.Int32BitsToSingle(bits);
        }

        return samples;
    }

    private static byte[] ReadAllBytes(Stream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        try
        {
            using var output = new MemoryStream();
            int bytesRead;
            while ((bytesRead = stream.Read(rentedBuffer, 0, rentedBuffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                output.Write(rentedBuffer, 0, bytesRead);
            }

            return output.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private static bool LooksLikeWav(ReadOnlySpan<byte> audioData)
    {
        return audioData.Length >= 12
            && audioData[0] == (byte)'R'
            && audioData[1] == (byte)'I'
            && audioData[2] == (byte)'F'
            && audioData[3] == (byte)'F'
            && audioData[8] == (byte)'W'
            && audioData[9] == (byte)'A'
            && audioData[10] == (byte)'V'
            && audioData[11] == (byte)'E';
    }

    private float[] PadOrTruncate(float[,] melSpec, int targetFrames)
    {
        int nMels = melSpec.GetLength(0);
        int nFrames = melSpec.GetLength(1);

        // Output shape: [1, 80, 3000]
        var result = new float[1 * MelBins * targetFrames];

        for (int mel = 0; mel < nMels; mel++)
        {
            for (int frame = 0; frame < targetFrames; frame++)
            {
                int idx = mel * targetFrames + frame;
                if (frame < nFrames)
                {
                    result[idx] = melSpec[mel, frame];
                }
                // else: remains 0.0f (default) which represents silence after normalization
            }
        }

        return result;
    }
}
