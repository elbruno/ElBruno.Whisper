namespace ElBruno.Whisper.Audio;

/// <summary>
/// Computes log-mel spectrograms using STFT and mel filterbanks.
/// </summary>
internal sealed class MelSpectrogramProcessor
{
    private readonly int _sampleRate;
    private readonly int _nMels;
    private readonly int _nFft;
    private readonly int _hopLength;
    private readonly float[] _window;
    private readonly float[,] _melFilterbank;

    public MelSpectrogramProcessor(int sampleRate, int nMels, int nFft, int hopLength)
    {
        _sampleRate = sampleRate;
        _nMels = nMels;
        _nFft = nFft;
        _hopLength = hopLength;
        _window = CreateHannWindow(nFft);
        _melFilterbank = CreateMelFilterbank(sampleRate, nFft, nMels);
    }

    public float[,] ComputeMelSpectrogram(float[] audio)
    {
        // Compute STFT
        var stft = ComputeStft(audio);

        // Compute magnitude spectrogram
        var magSpec = ComputeMagnitude(stft);

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

    private Complex[,] ComputeStft(float[] audio)
    {
        int nFrames = 1 + (audio.Length - _nFft) / _hopLength;
        if (nFrames < 0) nFrames = 0;

        var stft = new Complex[_nFft / 2 + 1, nFrames];

        for (int frameIdx = 0; frameIdx < nFrames; frameIdx++)
        {
            int start = frameIdx * _hopLength;
            var frame = new float[_nFft];

            // Extract and window frame
            for (int i = 0; i < _nFft && start + i < audio.Length; i++)
            {
                frame[i] = audio[start + i] * _window[i];
            }

            // Compute FFT
            var fft = Fft(frame);

            // Store magnitude (only first half + DC/Nyquist)
            for (int i = 0; i < _nFft / 2 + 1; i++)
            {
                stft[i, frameIdx] = fft[i];
            }
        }

        return stft;
    }

    private float[,] ComputeMagnitude(Complex[,] stft)
    {
        int nBins = stft.GetLength(0);
        int nFrames = stft.GetLength(1);
        var mag = new float[nBins, nFrames];

        for (int i = 0; i < nBins; i++)
        {
            for (int j = 0; j < nFrames; j++)
            {
                var m = stft[i, j].Magnitude;
                mag[i, j] = m * m;
            }
        }

        return mag;
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

    private static float[] CreateHannWindow(int size)
    {
        var window = new float[size];
        for (int i = 0; i < size; i++)
        {
            window[i] = 0.5f * (1 - MathF.Cos(2 * MathF.PI * i / (size - 1)));
        }
        return window;
    }

    private static float[,] CreateMelFilterbank(int sampleRate, int nFft, int nMels)
    {
        int nFreqs = nFft / 2 + 1;
        var filterbank = new float[nMels, nFreqs];

        // Mel scale conversion
        float HzToMel(float hz) => 2595.0f * MathF.Log10(1 + hz / 700.0f);
        float MelToHz(float mel) => 700.0f * (MathF.Pow(10, mel / 2595.0f) - 1);

        float minMel = HzToMel(0);
        float maxMel = HzToMel(sampleRate / 2.0f);

        // Create mel points
        var melPoints = new float[nMels + 2];
        for (int i = 0; i < nMels + 2; i++)
        {
            melPoints[i] = minMel + (maxMel - minMel) * i / (nMels + 1);
        }

        // Convert mel points to Hz and then to FFT bin indices
        var binPoints = new float[nMels + 2];
        for (int i = 0; i < nMels + 2; i++)
        {
            float hz = MelToHz(melPoints[i]);
            binPoints[i] = (nFft + 1) * hz / sampleRate;
        }

        // Create triangular filters
        for (int mel = 0; mel < nMels; mel++)
        {
            float left = binPoints[mel];
            float center = binPoints[mel + 1];
            float right = binPoints[mel + 2];

            for (int bin = 0; bin < nFreqs; bin++)
            {
                if (bin >= left && bin <= center)
                {
                    filterbank[mel, bin] = (bin - left) / (center - left);
                }
                else if (bin > center && bin <= right)
                {
                    filterbank[mel, bin] = (right - bin) / (right - center);
                }
            }
        }

        return filterbank;
    }

    // Simple Cooley-Tukey radix-2 FFT
    private static Complex[] Fft(float[] input)
    {
        int n = input.Length;
        
        // Pad to next power of 2
        int powerOf2 = 1;
        while (powerOf2 < n) powerOf2 *= 2;
        
        var x = new Complex[powerOf2];
        for (int i = 0; i < n; i++)
        {
            x[i] = new Complex(input[i], 0);
        }

        FftRecursive(x);
        return x;
    }

    private static void FftRecursive(Span<Complex> x)
    {
        int n = x.Length;
        if (n <= 1) return;

        // Divide
        var even = new Complex[n / 2];
        var odd = new Complex[n / 2];
        for (int i = 0; i < n / 2; i++)
        {
            even[i] = x[i * 2];
            odd[i] = x[i * 2 + 1];
        }

        // Conquer
        FftRecursive(even);
        FftRecursive(odd);

        // Combine
        for (int k = 0; k < n / 2; k++)
        {
            double angle = -2.0 * Math.PI * k / n;
            var t = new Complex(Math.Cos(angle), Math.Sin(angle)) * odd[k];
            x[k] = even[k] + t;
            x[k + n / 2] = even[k] - t;
        }
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
