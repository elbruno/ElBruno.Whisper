namespace ElBruno.Whisper.Audio;

/// <summary>
/// Computes log-mel spectrograms using STFT and mel filterbanks.
/// Matches the OpenAI Whisper reference implementation (centered STFT,
/// periodic Hann window, slaney-normalized mel filterbank).
/// </summary>
internal sealed class MelSpectrogramProcessor
{
    private readonly int _sampleRate;
    private readonly int _nMels;
    private readonly int _nFft;
    private readonly int _hopLength;
    private readonly float[] _window;
    private readonly float[,] _melFilterbank;
    private readonly double[,] _dftCosTable;
    private readonly double[,] _dftSinTable;

    public MelSpectrogramProcessor(int sampleRate, int nMels, int nFft, int hopLength)
    {
        _sampleRate = sampleRate;
        _nMels = nMels;
        _nFft = nFft;
        _hopLength = hopLength;
        _window = CreatePeriodicHannWindow(nFft);
        _melFilterbank = CreateMelFilterbank(sampleRate, nFft, nMels);

        // Pre-compute DFT twiddle factors for exact nFft-point DFT
        int nBins = nFft / 2 + 1;
        _dftCosTable = new double[nBins, nFft];
        _dftSinTable = new double[nBins, nFft];
        for (int k = 0; k < nBins; k++)
        {
            for (int n = 0; n < nFft; n++)
            {
                double angle = -2.0 * Math.PI * k * n / nFft;
                _dftCosTable[k, n] = Math.Cos(angle);
                _dftSinTable[k, n] = Math.Sin(angle);
            }
        }
    }

    public float[,] ComputeMelSpectrogram(float[] audio)
    {
        // Centered STFT matching torch.stft(audio, N_FFT, HOP_LENGTH, window=window, center=True)
        var stft = ComputeCenteredStft(audio);

        // Compute power spectrogram (magnitude squared)
        var magSpec = ComputePowerSpectrum(stft);

        // Apply mel filterbank
        var melSpec = ApplyMelFilterbank(magSpec);

        // Apply log10 (matching OpenAI Whisper reference)
        for (int i = 0; i < melSpec.GetLength(0); i++)
        {
            for (int j = 0; j < melSpec.GetLength(1); j++)
            {
                melSpec[i, j] = MathF.Log10(Math.Max(melSpec[i, j], 1e-10f));
            }
        }

        // Whisper normalization: clamp to within 8.0 of max, then scale to ~[-1, 1]
        float maxVal = float.MinValue;
        for (int i = 0; i < melSpec.GetLength(0); i++)
        {
            for (int j = 0; j < melSpec.GetLength(1); j++)
            {
                if (melSpec[i, j] > maxVal)
                    maxVal = melSpec[i, j];
            }
        }

        float clampFloor = maxVal - 8.0f;
        for (int i = 0; i < melSpec.GetLength(0); i++)
        {
            for (int j = 0; j < melSpec.GetLength(1); j++)
            {
                melSpec[i, j] = Math.Max(melSpec[i, j], clampFloor);
                melSpec[i, j] = (melSpec[i, j] + 4.0f) / 4.0f;
            }
        }

        return melSpec;
    }

    /// <summary>
    /// Centered STFT: pad audio by nFft/2 on each side, compute STFT, then drop the last frame.
    /// This matches torch.stft(audio, N_FFT, HOP_LENGTH, window=window, center=True) followed
    /// by stft[..., :-1] in the Whisper reference.
    /// </summary>
    private float[,] ComputeCenteredStft(float[] audio)
    {
        int pad = _nFft / 2;
        var padded = new float[audio.Length + 2 * pad];
        Array.Copy(audio, 0, padded, pad, audio.Length);

        int nBins = _nFft / 2 + 1;
        int totalFrames = 1 + (padded.Length - _nFft) / _hopLength;
        // Drop last frame to match Whisper's stft[..., :-1]
        int nFrames = totalFrames > 0 ? totalFrames - 1 : 0;

        var powerSpec = new float[nBins, nFrames];
        var frame = new float[_nFft];

        for (int frameIdx = 0; frameIdx < nFrames; frameIdx++)
        {
            int start = frameIdx * _hopLength;

            // Extract and window frame
            for (int i = 0; i < _nFft; i++)
            {
                frame[i] = (start + i < padded.Length) ? padded[start + i] * _window[i] : 0f;
            }

            // Compute exact nFft-point DFT for this frame and store power directly
            for (int k = 0; k < nBins; k++)
            {
                double re = 0, im = 0;
                for (int n = 0; n < _nFft; n++)
                {
                    re += frame[n] * _dftCosTable[k, n];
                    im += frame[n] * _dftSinTable[k, n];
                }
                powerSpec[k, frameIdx] = (float)(re * re + im * im);
            }
        }

        return powerSpec;
    }

    private float[,] ComputePowerSpectrum(float[,] stftPower)
    {
        // Already computed as power spectrum in ComputeCenteredStft
        return stftPower;
    }

    private float[,] ApplyMelFilterbank(float[,] magSpec)
    {
        int nBins = magSpec.GetLength(0);
        int nFrames = magSpec.GetLength(1);
        var melSpec = new float[_nMels, nFrames];

        for (int mel = 0; mel < _nMels; mel++)
        {
            for (int frame = 0; frame < nFrames; frame++)
            {
                float sum = 0;
                for (int bin = 0; bin < nBins; bin++)
                {
                    sum += magSpec[bin, frame] * _melFilterbank[mel, bin];
                }
                melSpec[mel, frame] = sum;
            }
        }

        return melSpec;
    }

    /// <summary>
    /// Periodic Hann window matching torch.hann_window(N_FFT).
    /// Divides by size (not size-1) to produce the periodic variant.
    /// </summary>
    private static float[] CreatePeriodicHannWindow(int size)
    {
        var window = new float[size];
        for (int i = 0; i < size; i++)
        {
            window[i] = 0.5f * (1 - MathF.Cos(2 * MathF.PI * i / size));
        }
        return window;
    }

    /// <summary>
    /// Creates a mel filterbank with slaney normalization matching librosa.filters.mel exactly.
    /// Uses the Slaney mel scale (linear below 1000 Hz, logarithmic above) and Hz-space interpolation.
    /// </summary>
    private static float[,] CreateMelFilterbank(int sampleRate, int nFft, int nMels)
    {
        int nFreqs = nFft / 2 + 1;
        var filterbank = new float[nMels, nFreqs];

        // Slaney mel scale (matches librosa default htk=False)
        const float fMin = 0.0f;
        const float fSp = 200.0f / 3.0f; // ~66.667 Hz per mel in linear region
        const float minLogHz = 1000.0f;
        float minLogMel = (minLogHz - fMin) / fSp; // = 15.0
        float logStep = MathF.Log(6.4f) / 27.0f;

        float HzToMel(float hz)
        {
            if (hz < minLogHz)
                return (hz - fMin) / fSp;
            return minLogMel + MathF.Log(hz / minLogHz) / logStep;
        }

        float MelToHz(float mel)
        {
            if (mel < minLogMel)
                return fMin + fSp * mel;
            return minLogHz * MathF.Exp(logStep * (mel - minLogMel));
        }

        // Create mel-spaced center frequencies (nMels + 2 for left/right edges)
        float minMel = HzToMel(0);
        float maxMel = HzToMel(sampleRate / 2.0f);
        var melFreqs = new float[nMels + 2];
        for (int i = 0; i < nMels + 2; i++)
        {
            melFreqs[i] = MelToHz(minMel + (maxMel - minMel) * i / (nMels + 1));
        }

        // FFT bin center frequencies
        var fftFreqs = new float[nFreqs];
        for (int i = 0; i < nFreqs; i++)
        {
            fftFreqs[i] = (float)sampleRate / 2.0f * i / (nFreqs - 1);
        }

        // Frequency differences between adjacent mel points
        var fdiff = new float[nMels + 1];
        for (int i = 0; i < nMels + 1; i++)
        {
            fdiff[i] = melFreqs[i + 1] - melFreqs[i];
        }

        // Build triangular filters in Hz space (matches librosa exactly)
        for (int mel = 0; mel < nMels; mel++)
        {
            for (int bin = 0; bin < nFreqs; bin++)
            {
                float lower = (fftFreqs[bin] - melFreqs[mel]) / fdiff[mel];
                float upper = (melFreqs[mel + 2] - fftFreqs[bin]) / fdiff[mel + 1];
                filterbank[mel, bin] = MathF.Max(0, MathF.Min(lower, upper));
            }

            // Slaney normalization: normalize by the width of the mel band
            float enorm = 2.0f / (melFreqs[mel + 2] - melFreqs[mel]);
            for (int bin = 0; bin < nFreqs; bin++)
            {
                filterbank[mel, bin] *= enorm;
            }
        }

        return filterbank;
    }

    private readonly struct Complex
    {
        public readonly double Real;
        public readonly double Imag;

        public Complex(double real, double imag)
        {
            Real = real;
            Imag = imag;
        }

        public float Magnitude => (float)Math.Sqrt(Real * Real + Imag * Imag);

        public static Complex operator +(Complex a, Complex b) =>
            new(a.Real + b.Real, a.Imag + b.Imag);

        public static Complex operator -(Complex a, Complex b) =>
            new(a.Real - b.Real, a.Imag - b.Imag);

        public static Complex operator *(Complex a, Complex b) =>
            new(a.Real * b.Real - a.Imag * b.Imag,
                a.Real * b.Imag + a.Imag * b.Real);
    }
}
