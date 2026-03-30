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
    private bool _disposed;

    private WhisperClient(
        WhisperOptions options,
        AudioProcessor audioProcessor,
        WhisperTokenizer tokenizer,
        WhisperInferenceSession inference)
    {
        _options = options;
        _audioProcessor = audioProcessor;
        _tokenizer = tokenizer;
        _inference = inference;
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

        return new WhisperClient(options, audioProcessor, tokenizer, inference);
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
            var startTime = DateTime.UtcNow;

            // Process audio to mel spectrogram
            var melSpec = _audioProcessor.ProcessAudioFile(audioFilePath);

            // Get initial tokens
            var initialTokens = GetInitialTokens();

            // Run inference
            var tokens = _inference.Inference(
                melSpec,
                initialTokens,
                _options.MaxTokens,
                _tokenizer.EotToken
            );

            // Decode tokens
            var text = _tokenizer.Decode(tokens);

            var duration = DateTime.UtcNow - startTime;

            return new TranscriptionResult
            {
                Text = text,
                DetectedLanguage = _options.Language,
                Duration = duration
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
            var startTime = DateTime.UtcNow;

            // Process audio to mel spectrogram
            var melSpec = _audioProcessor.ProcessAudioStream(audioStream);

            // Get initial tokens
            var initialTokens = GetInitialTokens();

            // Run inference
            var tokens = _inference.Inference(
                melSpec,
                initialTokens,
                _options.MaxTokens,
                _tokenizer.EotToken
            );

            // Decode tokens
            var text = _tokenizer.Decode(tokens);

            var duration = DateTime.UtcNow - startTime;

            return new TranscriptionResult
            {
                Text = text,
                DetectedLanguage = _options.Language,
                Duration = duration
            };
        }, cancellationToken);
    }

    private int[] GetInitialTokens()
    {
        var (sot, transcribe, translate, noTimestamps, language) = 
            _tokenizer.GetSpecialTokenIds(_options.Language);

        var tokens = new List<int> { sot };

        // Add language token if specified
        if (language.HasValue)
        {
            tokens.Add(language.Value);
        }

        // Add task token (transcribe or translate)
        tokens.Add(_options.Translate ? translate : transcribe);

        // Add no-timestamps token
        tokens.Add(noTimestamps);

        return tokens.ToArray();
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
