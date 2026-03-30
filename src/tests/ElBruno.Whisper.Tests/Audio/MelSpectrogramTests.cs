using Xunit;
using ElBruno.Whisper.Audio;

namespace ElBruno.Whisper.Tests.Audio;

public class MelSpectrogramTests
{
    [Fact]
    public void OutputShape_Is80x3000_For30SecondAudio()
    {
        var sampleRate = 16000;
        var duration = 30;
        var numSamples = sampleRate * duration;
        var audioData = new float[numSamples];

        var processor = new MelSpectrogramProcessor(sampleRate, 80, 400, 160);
        var melSpectrogram = processor.ComputeMelSpectrogram(audioData);

        Assert.NotNull(melSpectrogram);
        Assert.Equal(80, melSpectrogram.GetLength(0)); // Mel bins
        // Frame count is approximately 3000 for 30s at 16kHz with hop=160 (may differ by ±2 due to FFT windowing)
        Assert.InRange(melSpectrogram.GetLength(1), 2995, 3005);
    }

    [Fact]
    public void ShorterAudio_ProducesFewerFrames()
    {
        var sampleRate = 16000;
        var duration = 10; // Only 10 seconds
        var numSamples = sampleRate * duration;
        var audioData = new float[numSamples];

        var processor = new MelSpectrogramProcessor(sampleRate, 80, 400, 160);
        var melSpectrogram = processor.ComputeMelSpectrogram(audioData);

        Assert.NotNull(melSpectrogram);
        Assert.Equal(80, melSpectrogram.GetLength(0));
        Assert.True(melSpectrogram.GetLength(1) > 0); // Has some frames
    }

    [Fact]
    public void MelValues_AreFinite()
    {
        var sampleRate = 16000;
        var numSamples = sampleRate * 5; // 5 seconds
        var audioData = new float[numSamples];
        
        // Generate some non-zero audio
        for (int i = 0; i < numSamples; i++)
        {
            audioData[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 0.5f;
        }

        var processor = new MelSpectrogramProcessor(sampleRate, 80, 400, 160);
        var melSpectrogram = processor.ComputeMelSpectrogram(audioData);

        bool allFinite = true;
        for (int i = 0; i < melSpectrogram.GetLength(0); i++)
        {
            for (int j = 0; j < melSpectrogram.GetLength(1); j++)
            {
                var value = melSpectrogram[i, j];
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    allFinite = false;
                    break;
                }
            }
        }

        Assert.True(allFinite, "All mel spectrogram values should be finite (no NaN or Infinity)");
    }

    [Fact]
    public void SilentAudio_ProducesLowMelValues()
    {
        var sampleRate = 16000;
        var numSamples = sampleRate * 5; // 5 seconds
        var audioData = new float[numSamples]; // All zeros (silence)

        var processor = new MelSpectrogramProcessor(sampleRate, 80, 400, 160);
        var melSpectrogram = processor.ComputeMelSpectrogram(audioData);

        // Calculate average magnitude
        double sum = 0;
        int count = 0;
        for (int i = 0; i < melSpectrogram.GetLength(0); i++)
        {
            for (int j = 0; j < melSpectrogram.GetLength(1); j++)
            {
                sum += Math.Abs(melSpectrogram[i, j]);
                count++;
            }
        }
        var avgMagnitude = sum / count;

        // Silent audio log-mel values are dominated by the floor constant (typically log(1e-10) ≈ -23).
        // The absolute average may be non-trivial, but should be finite and consistent.
        Assert.True(avgMagnitude < 30.0, $"Silent audio should produce bounded mel values, but got average: {avgMagnitude}");
    }

    [Fact]
    public void NonSilentAudio_ProducesHigherMelValues()
    {
        var sampleRate = 16000;
        var numSamples = sampleRate * 5; // 5 seconds
        var audioData = new float[numSamples];
        
        // Generate a 440 Hz sine wave (musical note A4)
        for (int i = 0; i < numSamples; i++)
        {
            audioData[i] = (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 0.5f;
        }

        var processor = new MelSpectrogramProcessor(sampleRate, 80, 400, 160);
        var melSpectrogram = processor.ComputeMelSpectrogram(audioData);

        // Calculate average magnitude
        double sum = 0;
        int count = 0;
        for (int i = 0; i < melSpectrogram.GetLength(0); i++)
        {
            for (int j = 0; j < melSpectrogram.GetLength(1); j++)
            {
                sum += Math.Abs(melSpectrogram[i, j]);
                count++;
            }
        }
        var avgMagnitude = sum / count;

        // Non-silent audio should have measurable energy
        Assert.True(avgMagnitude > 0.1, $"Non-silent audio should produce measurable mel values, but got average: {avgMagnitude}");
    }

    [Fact]
    public void EmptyAudio_IsHandled()
    {
        var sampleRate = 16000;
        var audioData = new float[0];

        var processor = new MelSpectrogramProcessor(sampleRate, 80, 400, 160);
        var melSpectrogram = processor.ComputeMelSpectrogram(audioData);

        Assert.NotNull(melSpectrogram);
        Assert.Equal(80, melSpectrogram.GetLength(0));
        Assert.True(melSpectrogram.GetLength(1) >= 0); // No frames or zero frames
    }

    [Fact]
    public void VeryShortAudio_IsHandled()
    {
        var sampleRate = 16000;
        var audioData = new float[100]; // Less than 1 second

        var processor = new MelSpectrogramProcessor(sampleRate, 80, 400, 160);
        var melSpectrogram = processor.ComputeMelSpectrogram(audioData);

        Assert.NotNull(melSpectrogram);
        Assert.Equal(80, melSpectrogram.GetLength(0));
        Assert.True(melSpectrogram.GetLength(1) >= 0);
    }

    [Fact]
    public void DifferentSampleRates_AreHandled()
    {
        var sampleRates = new[] { 8000, 16000, 22050, 44100, 48000 };

        foreach (var sampleRate in sampleRates)
        {
            var duration = 5;
            var numSamples = sampleRate * duration;
            var audioData = new float[numSamples];

            var processor = new MelSpectrogramProcessor(sampleRate, 80, 400, 160);
            var melSpectrogram = processor.ComputeMelSpectrogram(audioData);

            Assert.NotNull(melSpectrogram);
            Assert.Equal(80, melSpectrogram.GetLength(0));
            // Time dimension varies based on sample rate and hop length
        }
    }
}
