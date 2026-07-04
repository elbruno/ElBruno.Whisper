using System.Runtime.CompilerServices;
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
    private readonly IWhisperTranscriptionBackend _transcriptionBackend;
    private bool _disposed;

    internal WhisperClient(
        WhisperOptions options,
        AudioProcessor audioProcessor,
        IWhisperTranscriptionBackend transcriptionBackend)
    {
        _options = options;
        _audioProcessor = audioProcessor;
        _transcriptionBackend = transcriptionBackend;
    }

    internal WhisperOptions Options => _options;

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
        var transcriptionBackend = new WhisperTranscriptionBackend(options, runtime);

        return new WhisperClient(options, audioProcessor, transcriptionBackend);
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
            ct => _audioProcessor.ProcessAudioFile(audioFilePath, ct),
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
            ct => _audioProcessor.ProcessAudioStream(audioStream, ct),
            cancellationToken);
    }

    /// <summary>
    /// Transcribe audio from a raw PCM stream using an explicit format description.
    /// WAV streams are detected automatically from the header and do not require a format.
    /// </summary>
    public Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        WhisperAudioFormat format,
        CancellationToken cancellationToken = default)
    {
        return TranscribeCoreAsync(
            ct => _audioProcessor.ProcessAudioStream(audioStream, format, ct),
            cancellationToken);
    }

    /// <summary>
    /// Transcribe audio from raw PCM bytes using an explicit format description.
    /// </summary>
    public Task<TranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<byte> audioData,
        WhisperAudioFormat format,
        CancellationToken cancellationToken = default)
    {
        return TranscribeCoreAsync(
            ct => _audioProcessor.ProcessAudioBytes(audioData, format, ct),
            cancellationToken);
    }

    /// <summary>
    /// Transcribe mono float samples using the provided sample rate.
    /// </summary>
    public Task<TranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<float> monoAudio,
        int sampleRate,
        CancellationToken cancellationToken = default)
    {
        return TranscribeCoreAsync(
            ct => _audioProcessor.ProcessAudioSamples(monoAudio, sampleRate, ct),
            cancellationToken);
    }

    /// <summary>
    /// Transcribe float PCM samples using an explicit format description.
    /// </summary>
    public Task<TranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<float> audioData,
        WhisperAudioFormat format,
        CancellationToken cancellationToken = default)
    {
        return TranscribeCoreAsync(
            ct => _audioProcessor.ProcessAudioSamples(audioData, format, ct),
            cancellationToken);
    }

    /// <summary>
    /// Produce rolling transcription updates for an audio file.
    /// </summary>
    public IAsyncEnumerable<StreamingTranscriptionUpdate> GetStreamingTextAsync(
        string audioFilePath,
        WhisperStreamingOptions? streamingOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
        {
            throw new FileNotFoundException("Audio file not found", audioFilePath);
        }

        return GetStreamingTextCoreAsync(
            _audioProcessor.ReadAudioFile(audioFilePath, cancellationToken),
            streamingOptions ?? new WhisperStreamingOptions(),
            cancellationToken);
    }

    /// <summary>
    /// Produce rolling transcription updates for an audio stream.
    /// </summary>
    public IAsyncEnumerable<StreamingTranscriptionUpdate> GetStreamingTextAsync(
        Stream audioStream,
        WhisperStreamingOptions? streamingOptions = null,
        CancellationToken cancellationToken = default)
    {
        return GetStreamingTextCoreAsync(
            _audioProcessor.ReadAudioStream(audioStream, cancellationToken),
            streamingOptions ?? new WhisperStreamingOptions(),
            cancellationToken);
    }

    /// <summary>
    /// Release ONNX Runtime resources held by the client.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _transcriptionBackend.Dispose();
            _disposed = true;
        }
    }

    private Task<TranscriptionResult> TranscribeCoreAsync(
        Func<CancellationToken, (float[] MelSpectrogram, TimeSpan Duration)> audioProcessor,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (melSpec, audioDuration) = audioProcessor(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return await _transcriptionBackend
                .TranscribeAsync(melSpec, audioDuration, cancellationToken)
                .ConfigureAwait(false);
        }, cancellationToken);
    }

    private async IAsyncEnumerable<StreamingTranscriptionUpdate> GetStreamingTextCoreAsync(
        ProcessedAudio processedAudio,
        WhisperStreamingOptions streamingOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        streamingOptions.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var state = new StreamingTranscriptState(
            processedAudio.Duration,
            streamingOptions.GetRequiredHypothesisCount(),
            streamingOptions.UseLocalAgreement);

        foreach (var window in GetRollingWindows(processedAudio.Samples.Length, streamingOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var windowSamples = SliceSamples(processedAudio.Samples, window.StartSample, window.EndSample);
            var (melSpectrogram, audioDuration) = _audioProcessor.ProcessNormalizedSamples(windowSamples);
            var result = await _transcriptionBackend
                .TranscribeAsync(melSpectrogram, audioDuration, cancellationToken)
                .ConfigureAwait(false);

            var update = state.AddHypothesis(result.Text, window.Start, window.End);
            if (update is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return update;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        yield return state.BuildFinalUpdate();
    }

    private static IReadOnlyList<RollingWindow> GetRollingWindows(
        int totalSamples,
        WhisperStreamingOptions streamingOptions)
    {
        if (totalSamples <= 0)
        {
            return [];
        }

        var windows = new List<RollingWindow>();
        var windowSamples = ToSampleCount(streamingOptions.WindowSize);
        var stepSamples = ToSampleCount(streamingOptions.StepSize);
        var overlapSamples = ToSampleCount(streamingOptions.ContextOverlap);

        for (int cursorSample = 0; cursorSample < totalSamples; cursorSample += stepSamples)
        {
            var startSample = Math.Max(0, cursorSample - overlapSamples);
            var endSample = Math.Min(totalSamples, cursorSample + windowSamples);
            windows.Add(new RollingWindow(startSample, endSample));

            if (endSample >= totalSamples)
            {
                break;
            }
        }

        return windows;
    }

    private static int ToSampleCount(TimeSpan duration)
    {
        return Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds * AudioProcessor.TargetSampleRate));
    }

    private static float[] SliceSamples(float[] source, int startSample, int endSample)
    {
        var length = Math.Max(0, endSample - startSample);
        if (length == 0)
        {
            return [];
        }

        var result = new float[length];
        Array.Copy(source, startSample, result, 0, length);
        return result;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(WhisperClient));
    }

    private readonly record struct RollingWindow(int StartSample, int EndSample)
    {
        public TimeSpan Start => TimeSpan.FromSeconds((double)StartSample / AudioProcessor.TargetSampleRate);

        public TimeSpan End => TimeSpan.FromSeconds((double)EndSample / AudioProcessor.TargetSampleRate);
    }

    private sealed class StreamingTranscriptState
    {
        private readonly List<string> _committedWords = [];
        private readonly List<IReadOnlyList<string>> _hypothesisQueue = [];
        private readonly TimeSpan _totalDuration;
        private readonly int _requiredHypothesisCount;
        private readonly bool _useLocalAgreement;
        private string _lastCommittedText = string.Empty;
        private string _lastProvisionalText = string.Empty;

        public StreamingTranscriptState(
            TimeSpan totalDuration,
            int requiredHypothesisCount,
            bool useLocalAgreement)
        {
            _totalDuration = totalDuration;
            _requiredHypothesisCount = requiredHypothesisCount;
            _useLocalAgreement = useLocalAgreement;
        }

        public StreamingTranscriptionUpdate? AddHypothesis(string hypothesisText, TimeSpan windowStart, TimeSpan windowEnd)
        {
            _hypothesisQueue.Add(SplitWords(hypothesisText));
            CommitStableHypotheses();

            var committedText = JoinWords(_committedWords);
            var provisionalText = JoinWords(MergeQueuedWords(_hypothesisQueue));

            if (committedText == _lastCommittedText && provisionalText == _lastProvisionalText)
            {
                return null;
            }

            _lastCommittedText = committedText;
            _lastProvisionalText = provisionalText;

            return CreateUpdate(committedText, provisionalText, windowStart, windowEnd, isFinal: false);
        }

        public StreamingTranscriptionUpdate BuildFinalUpdate()
        {
            var remainingWords = MergeQueuedWords(_hypothesisQueue);
            AppendWithOverlap(_committedWords, remainingWords);
            _hypothesisQueue.Clear();

            var committedText = JoinWords(_committedWords);
            return CreateUpdate(
                committedText,
                string.Empty,
                TimeSpan.Zero,
                _totalDuration,
                isFinal: true);
        }

        private void CommitStableHypotheses()
        {
            while (_hypothesisQueue.Count >= _requiredHypothesisCount)
            {
                if (_useLocalAgreement && !HasAgreement(_hypothesisQueue, _requiredHypothesisCount))
                {
                    break;
                }

                var oldestHypothesis = _hypothesisQueue[0];
                AppendWithOverlap(_committedWords, oldestHypothesis);
                _hypothesisQueue.RemoveAt(0);

                if (_hypothesisQueue.Count == 0)
                {
                    continue;
                }

                var overlap = FindSuffixPrefixOverlap(oldestHypothesis, _hypothesisQueue[0]);
                if (overlap <= 0)
                {
                    continue;
                }

                var trimmed = _hypothesisQueue[0].Skip(overlap).ToArray();
                _hypothesisQueue[0] = trimmed;

                if (trimmed.Length > 0)
                {
                    continue;
                }

                _hypothesisQueue.RemoveAt(0);
            }
        }

        private static bool HasAgreement(IReadOnlyList<IReadOnlyList<string>> hypotheses, int requiredHypothesisCount)
        {
            for (int index = 0; index < requiredHypothesisCount - 1; index++)
            {
                if (FindSuffixPrefixOverlap(hypotheses[index], hypotheses[index + 1]) <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private StreamingTranscriptionUpdate CreateUpdate(
            string committedText,
            string provisionalText,
            TimeSpan windowStart,
            TimeSpan windowEnd,
            bool isFinal)
        {
            var combinedText = string.IsNullOrWhiteSpace(provisionalText)
                ? committedText
                : string.IsNullOrWhiteSpace(committedText)
                    ? provisionalText
                    : $"{committedText} {provisionalText}";

            return new StreamingTranscriptionUpdate
            {
                Text = combinedText,
                CommittedText = committedText,
                ProvisionalText = provisionalText,
                WindowStart = windowStart,
                WindowEnd = windowEnd,
                TotalDuration = _totalDuration,
                IsFinal = isFinal
            };
        }

        private static IReadOnlyList<string> MergeQueuedWords(IEnumerable<IReadOnlyList<string>> hypotheses)
        {
            var merged = new List<string>();
            foreach (var hypothesis in hypotheses)
            {
                AppendWithOverlap(merged, hypothesis);
            }

            return merged;
        }

        private static IReadOnlyList<string> SplitWords(string value)
        {
            return value.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string JoinWords(IEnumerable<string> words)
        {
            return string.Join(" ", words);
        }

        private static void AppendWithOverlap(List<string> destination, IReadOnlyList<string> source)
        {
            if (source.Count == 0)
            {
                return;
            }

            var overlap = FindSuffixPrefixOverlap(destination, source);
            for (int index = overlap; index < source.Count; index++)
            {
                destination.Add(source[index]);
            }
        }

        private static int FindSuffixPrefixOverlap(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            var maxOverlap = Math.Min(left.Count, right.Count);
            for (int length = maxOverlap; length > 0; length--)
            {
                var matches = true;
                for (int offset = 0; offset < length; offset++)
                {
                    if (!string.Equals(
                            left[left.Count - length + offset],
                            right[offset],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return length;
                }
            }

            return 0;
        }
    }
}
