using ElBruno.HuggingFace;
using ElBruno.Whisper.Audio;
using ElBruno.Whisper.Inference;

namespace ElBruno.Whisper;

/// <summary>
/// Main client for Whisper speech-to-text transcription and translation.
/// The client is safe to share across concurrent callers when configured through
/// <see cref="WhisperOptions.Concurrency"/>.
/// </summary>
public sealed class WhisperClient : IDisposable
{
    private readonly WhisperOptions _options;
    private readonly AudioProcessor _audioProcessor;
    private readonly WhisperModelRuntime _runtime;
    private bool _disposed;

    private WhisperClient(
        WhisperOptions options,
        AudioProcessor audioProcessor,
        WhisperModelRuntime runtime)
    {
        _options = options;
        _audioProcessor = audioProcessor;
        _runtime = runtime;
    }

    /// <summary>
    /// Create a new WhisperClient instance with automatic model download if needed.
    /// </summary>
    public static async Task<WhisperClient> CreateAsync(
        WhisperOptions? options = null,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new WhisperOptions();
        var audioProcessor = new AudioProcessor();
        var runtime = await WhisperModelRuntime.CreateAsync(options, progress, cancellationToken)
            .ConfigureAwait(false);

        return new WhisperClient(options, audioProcessor, runtime);
    }

    /// <summary>
    /// Transcribe audio from a file.
    /// </summary>
    public Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
            throw new FileNotFoundException("Audio file not found", audioFilePath);

        return TranscribeCoreAsync(
            () => _audioProcessor.ProcessAudioFile(audioFilePath),
            cancellationToken);
    }

    /// <summary>
    /// Transcribe audio from a stream.
    /// </summary>
    public Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        CancellationToken cancellationToken = default)
    {
        return TranscribeCoreAsync(
            () => _audioProcessor.ProcessAudioStream(audioStream),
            cancellationToken);
    }

    private TranscriptionResult BuildResult(int[] tokens, TimeSpan audioDuration)
    {
        if (_options.EnableTimestamps)
        {
            var (text, decodedSegments) = _runtime.Tokenizer.DecodeWithTimestamps(tokens);
            var segments = decodedSegments.Count > 0 || string.IsNullOrWhiteSpace(text)
                ? decodedSegments
                : new List<TranscriptionSegment>
                {
                    _runtime.Tokenizer.CreateTimedSegment(TimeSpan.Zero, audioDuration, text)
                };

            return new TranscriptionResult
            {
                Text = text,
                DetectedLanguage = _options.Language,
                Duration = audioDuration,
                Segments = segments,
                Words = segments.SelectMany(static segment => segment.Words).ToArray()
            };
        }

        return new TranscriptionResult
        {
            Text = _runtime.Tokenizer.Decode(tokens),
            DetectedLanguage = _options.Language,
            Duration = audioDuration
        };
    }

    private int[] GetInitialTokens()
    {
        var (sot, transcribe, translate, noTimestamps, language) = 
            _runtime.Tokenizer.GetSpecialTokenIds(_options.Language);

        var tokens = new List<int> { sot };

        // Add language and task tokens only when language is specified.
        // For English-only (.en) models without explicit language, the Whisper
        // reference uses just [SOT, noTimestamps] — no language or task tokens.
        if (language.HasValue)
        {
            tokens.Add(language.Value);
            tokens.Add(_options.Translate ? translate : transcribe);
        }

        // Skip no-timestamps token when timestamps are enabled so the model
        // generates timestamp tokens in its output sequence.
        if (!_options.EnableTimestamps)
        {
            tokens.Add(noTimestamps);
        }

        return tokens.ToArray();
    }

    /// <summary>
    /// Builds the list of token IDs to suppress during decoding.
    /// Combines the model's config suppress_tokens with timestamp suppression.
    /// EOT is NOT suppressed here (it's the stop condition).
    /// </summary>
    private int[] GetSuppressTokens()
    {
        var suppress = new HashSet<int>(_runtime.ConfigSuppressTokens);

        // Only suppress timestamp tokens when timestamps are disabled
        if (!_options.EnableTimestamps)
        {
            var (_, _, _, noTimestamps, _) = _runtime.Tokenizer.GetSpecialTokenIds();
            const int vocabSize = 51865;
            for (int t = noTimestamps + 1; t < vocabSize; t++)
            {
                suppress.Add(t);
            }
        }

        return suppress.ToArray();
    }

    /// <summary>
    /// Release ONNX Runtime resources held by the client.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _runtime.Dispose();
            _disposed = true;
        }
    }

    private Task<TranscriptionResult> TranscribeCoreAsync(
        Func<(float[] MelSpectrogram, TimeSpan Duration)> audioProcessor,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (melSpec, audioDuration) = audioProcessor();
            cancellationToken.ThrowIfCancellationRequested();

            var initialTokens = GetInitialTokens();
            var suppressTokens = GetSuppressTokens();
            var tokens = await _runtime.RunInferenceAsync(
                    melSpec,
                    initialTokens,
                    _options.MaxTokens,
                    _runtime.Tokenizer.EotToken,
                    suppressTokens,
                    _runtime.BeginSuppressTokens,
                    cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            return BuildResult(tokens, audioDuration);
        }, cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(WhisperClient));
    }
}
