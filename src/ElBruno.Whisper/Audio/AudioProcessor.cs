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
    private const float MinLogValue = 1e-10f;

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
    public float[] ProcessAudioFile(string path)
    {
        var wav = WavReader.FromFile(path);
        return ProcessAudio(wav);
    }

    /// <summary>
    /// Process a WAV stream into a log-mel spectrogram tensor [1, 80, 3000].
    /// </summary>
    public float[] ProcessAudioStream(Stream stream)
    {
        var wav = WavReader.FromStream(stream);
        return ProcessAudio(wav);
    }

    private float[] ProcessAudio(WavReader wav)
    {
        // Convert to mono and resample to 16kHz
        wav.ConvertToMono();
        wav.Resample(TargetSampleRate);

        // Compute log-mel spectrogram
        var melSpec = _melProcessor.ComputeMelSpectrogram(wav.Samples);

        // Pad or truncate to 3000 frames (30 seconds)
        var paddedSpec = PadOrTruncate(melSpec, MaxFrames);

        return paddedSpec;
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
                else
                {
                    result[idx] = MinLogValue; // Pad with very small value (log of near-zero)
                }
            }
        }

        return result;
    }
}
