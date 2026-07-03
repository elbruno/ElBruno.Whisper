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
        var processedAudio = ReadAudioFile(path);
        return ProcessNormalizedSamples(processedAudio.Samples, processedAudio.Duration);
    }

    /// <summary>
    /// Process a WAV stream into a log-mel spectrogram tensor [1, 80, 3000].
    /// </summary>
    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessAudioStream(Stream stream)
    {
        var processedAudio = ReadAudioStream(stream);
        return ProcessNormalizedSamples(processedAudio.Samples, processedAudio.Duration);
    }

    public ProcessedAudio ReadAudioFile(string path)
    {
        var wav = WavReader.FromFile(path);
        return ReadAudio(wav);
    }

    public ProcessedAudio ReadAudioStream(Stream stream)
    {
        var wav = WavReader.FromStream(stream);
        return ReadAudio(wav);
    }

    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessNormalizedSamples(float[] samples)
    {
        var audioDuration = TimeSpan.FromSeconds((double)samples.Length / TargetSampleRate);
        return ProcessNormalizedSamples(samples, audioDuration);
    }

    public (float[] MelSpectrogram, TimeSpan AudioDuration) ProcessNormalizedSamples(
        float[] samples,
        TimeSpan audioDuration)
    {
        var paddedAudio = PadOrTrimAudio(samples, MaxSamples);
        var melSpec = _melProcessor.ComputeMelSpectrogram(paddedAudio);
        var result = PadOrTruncate(melSpec, MaxFrames);

        return (result, audioDuration);
    }

    private static ProcessedAudio ReadAudio(WavReader wav)
    {
        wav.ConvertToMono();
        wav.Resample(TargetSampleRate);

        var audioDuration = TimeSpan.FromSeconds((double)wav.Samples.Length / TargetSampleRate);
        return new ProcessedAudio(wav.Samples, audioDuration);
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
