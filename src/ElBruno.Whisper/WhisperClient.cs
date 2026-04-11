using System.Text.Json;
using ElBruno.HuggingFace;
using ElBruno.Whisper.Audio;
using ElBruno.Whisper.Inference;
using ElBruno.Whisper.Tokenizer;

namespace ElBruno.Whisper;

/// <summary>
/// Main client for Whisper speech-to-text transcription and translation.
/// </summary>
public sealed class WhisperClient : IDisposable
{
    private readonly WhisperOptions _options;
    private readonly AudioProcessor _audioProcessor;
    private readonly WhisperTokenizer _tokenizer;
    private readonly WhisperInferenceSession _inference;
    private readonly int[] _configSuppressTokens;
    private readonly int[] _beginSuppressTokens;
    private bool _disposed;

    private WhisperClient(
        WhisperOptions options,
        AudioProcessor audioProcessor,
        WhisperTokenizer tokenizer,
        WhisperInferenceSession inference,
        int[] configSuppressTokens,
        int[] beginSuppressTokens)
    {
        _options = options;
        _audioProcessor = audioProcessor;
        _tokenizer = tokenizer;
        _inference = inference;
        _configSuppressTokens = configSuppressTokens;
        _beginSuppressTokens = beginSuppressTokens;
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

        // Resolve model path
        string modelPath;
        if (!string.IsNullOrEmpty(options.ModelPath))
        {
            modelPath = options.ModelPath;
        }
        else
        {
            // Use cache directory
            var cacheDir = options.CacheDirectory ?? DefaultPathHelper.GetDefaultCacheDirectory("ElBruno/Whisper");
            modelPath = Path.Combine(cacheDir, options.Model.Id);

            // Download model if needed
            if (options.EnsureModelDownloaded)
            {
                await DownloadModelAsync(options.Model, cacheDir, progress, cancellationToken);
            }
        }

        // Load components
        var audioProcessor = new AudioProcessor();
        
        var tokenizerPath = Path.Combine(modelPath, "tokenizer.json");
        var tokenizer = new WhisperTokenizer(tokenizerPath);

        var encoderPath = Path.Combine(modelPath, "onnx", "encoder_model.onnx");
        var decoderPath = Path.Combine(modelPath, "onnx", "decoder_model_merged.onnx");
        var inference = new WhisperInferenceSession(
            encoderPath, decoderPath,
            options.Model.NumDecoderLayers,
            options.Model.EncoderDimension);

        // Load suppress_tokens and begin_suppress_tokens from config.json
        var (configSuppressTokens, beginSuppressTokens) = LoadSuppressConfig(modelPath);

        return new WhisperClient(options, audioProcessor, tokenizer, inference,
            configSuppressTokens, beginSuppressTokens);
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

        return Task.Run(() =>
        {
            // Process audio to mel spectrogram
            var (melSpec, audioDuration) = _audioProcessor.ProcessAudioFile(audioFilePath);

            // Get initial tokens
            var initialTokens = GetInitialTokens();

            // Run inference
            var tokens = _inference.Inference(
                melSpec,
                initialTokens,
                _options.MaxTokens,
                _tokenizer.EotToken,
                GetSuppressTokens(),
                _beginSuppressTokens
            );

            // Decode tokens
            var text = _tokenizer.Decode(tokens);

            return new TranscriptionResult
            {
                Text = text,
                DetectedLanguage = _options.Language,
                Duration = audioDuration
            };
        }, cancellationToken);
    }

    /// <summary>
    /// Transcribe audio from a stream.
    /// </summary>
    public Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            // Process audio to mel spectrogram
            var (melSpec, audioDuration) = _audioProcessor.ProcessAudioStream(audioStream);

            // Get initial tokens
            var initialTokens = GetInitialTokens();

            // Run inference
            var tokens = _inference.Inference(
                melSpec,
                initialTokens,
                _options.MaxTokens,
                _tokenizer.EotToken,
                GetSuppressTokens(),
                _beginSuppressTokens
            );

            // Decode tokens
            var text = _tokenizer.Decode(tokens);

            return new TranscriptionResult
            {
                Text = text,
                DetectedLanguage = _options.Language,
                Duration = audioDuration
            };
        }, cancellationToken);
    }

    private int[] GetInitialTokens()
    {
        var (sot, transcribe, translate, noTimestamps, language) = 
            _tokenizer.GetSpecialTokenIds(_options.Language);

        var tokens = new List<int> { sot };

        // Add language and task tokens only when language is specified.
        // For English-only (.en) models without explicit language, the Whisper
        // reference uses just [SOT, noTimestamps] — no language or task tokens.
        if (language.HasValue)
        {
            tokens.Add(language.Value);
            tokens.Add(_options.Translate ? translate : transcribe);
        }

        // Add no-timestamps token
        tokens.Add(noTimestamps);

        return tokens.ToArray();
    }

    /// <summary>
    /// Builds the list of token IDs to suppress during decoding.
    /// Combines the model's config suppress_tokens with timestamp suppression.
    /// EOT is NOT suppressed here (it's the stop condition).
    /// </summary>
    private int[] GetSuppressTokens()
    {
        var suppress = new HashSet<int>(_configSuppressTokens);

        // Suppress timestamp tokens (all tokens from noTimestamps+1 onward)
        var (_, _, _, noTimestamps, _) = _tokenizer.GetSpecialTokenIds();
        const int vocabSize = 51865;
        for (int t = noTimestamps + 1; t < vocabSize; t++)
        {
            suppress.Add(t);
        }

        return suppress.ToArray();
    }

    /// <summary>
    /// Reads suppress_tokens and begin_suppress_tokens from the model's config.json.
    /// Falls back to reasonable defaults if config.json is not available.
    /// </summary>
    private static (int[] SuppressTokens, int[] BeginSuppressTokens) LoadSuppressConfig(string modelPath)
    {
        var configPath = Path.Combine(modelPath, "config.json");
        if (!File.Exists(configPath))
        {
            return (Array.Empty<int>(), new[] { 220, 50256 });
        }

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var suppressTokens = Array.Empty<int>();
            if (root.TryGetProperty("suppress_tokens", out var suppressProp))
            {
                suppressTokens = suppressProp.EnumerateArray()
                    .Select(e => e.GetInt32())
                    .ToArray();
            }

            var beginSuppressTokens = new[] { 220, 50256 };
            if (root.TryGetProperty("begin_suppress_tokens", out var beginProp))
            {
                beginSuppressTokens = beginProp.EnumerateArray()
                    .Select(e => e.GetInt32())
                    .ToArray();
            }

            return (suppressTokens, beginSuppressTokens);
        }
        catch
        {
            return (Array.Empty<int>(), new[] { 220, 50256 });
        }
    }

    private static async Task DownloadModelAsync(
        WhisperModelDefinition model,
        string cacheDir,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var modelDir = Path.Combine(cacheDir, model.Id);

        // Check if already downloaded
        var allFilesExist = model.RequiredFiles.All(f => File.Exists(Path.Combine(modelDir, f)));
        if (allFilesExist)
            return;

        using var downloader = new HuggingFaceDownloader();
        var request = new DownloadRequest
        {
            RepoId = model.HuggingFaceRepoId,
            LocalDirectory = modelDir,
            RequiredFiles = model.RequiredFiles,
            OptionalFiles = model.OptionalFiles,
            Progress = progress
        };

        await downloader.DownloadFilesAsync(request, cancellationToken);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _inference?.Dispose();
            _disposed = true;
        }
    }
}
