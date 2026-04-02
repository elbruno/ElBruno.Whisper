namespace ElBruno.Whisper.Audio;

/// <summary>
/// Processes audio files into log-mel spectrograms for Whisper inference.
/// </summary>
internal sealed class AudioProcessor
{
    private const int TargetSampleRate = 16000;
    private const int MelBins = 80;
    private const int FftSize = 400; // 25ms at 16kHz
    private const int HopLength = 160; // 10ms at 16kHz
    private const int MaxFrames = 3000; // 30 seconds
    private const int MaxSamples = TargetSampleRate * 30; // 480,000 samples = 30 seconds

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
    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessAudioFile(string path)
    {
        var wav = WavReader.FromFile(path);
        return ProcessAudio(wav);
    }

    /// <summary>
    /// Process a WAV stream into a log-mel spectrogram tensor [1, 80, 3000].
    /// </summary>
    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessAudioStream(Stream stream)
    {
        var wav = WavReader.FromStream(stream);
        return ProcessAudio(wav);
    }

    private (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessAudio(WavReader wav)
    {
        // Convert to mono and resample to 16kHz
        wav.ConvertToMono();
        wav.Resample(TargetSampleRate);

        // Compute audio duration before padding
        var audioDuration = TimeSpan.FromSeconds((double)wav.Samples.Length / TargetSampleRate);

        // Pad or trim audio to exactly 30 seconds (480,000 samples)
        // This matches OpenAI Whisper's behavior and ensures STFT produces exactly 3000 frames
        var paddedAudio = PadOrTrimAudio(wav.Samples, MaxSamples);

        // Compute log-mel spectrogram
        var melSpec = _melProcessor.ComputeMelSpectrogram(paddedAudio);

        // Flatten [80, nFrames] to [1, 80, 3000] with padding if needed
        var result = PadOrTruncate(melSpec, MaxFrames);

        return (result, audioDuration);
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
