using ElBruno.Whisper.Inference;

namespace ElBruno.Whisper;

internal sealed class WhisperTranscriptionBackend : IWhisperTranscriptionBackend
{
    private readonly WhisperOptions _options;
    private readonly WhisperModelRuntime _runtime;
    private bool _disposed;

    public WhisperTranscriptionBackend(WhisperOptions options, WhisperModelRuntime runtime)
    {
        _options = options;
        _runtime = runtime;
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        float[] melSpectrogram,
        TimeSpan audioDuration,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var initialTokens = GetInitialTokens();
        var suppressTokens = GetSuppressTokens();
        var tokens = await _runtime.RunInferenceAsync(
                melSpectrogram,
                initialTokens,
                _options.MaxTokens,
                _runtime.Tokenizer.EotToken,
                suppressTokens,
                _runtime.BeginSuppressTokens,
                cancellationToken)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return BuildResult(tokens, audioDuration);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _runtime.Dispose();
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

        if (language.HasValue)
        {
            tokens.Add(language.Value);
            tokens.Add(_options.Translate ? translate : transcribe);
        }

        if (!_options.EnableTimestamps)
        {
            tokens.Add(noTimestamps);
        }

        return tokens.ToArray();
    }

    private int[] GetSuppressTokens()
    {
        var suppress = new HashSet<int>(_runtime.ConfigSuppressTokens);

        if (!_options.EnableTimestamps)
        {
            var (_, _, _, noTimestamps, _) = _runtime.Tokenizer.GetSpecialTokenIds();
            const int vocabSize = 51865;
            for (int token = noTimestamps + 1; token < vocabSize; token++)
            {
                suppress.Add(token);
            }
        }

        return suppress.ToArray();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(WhisperTranscriptionBackend));
    }
}
