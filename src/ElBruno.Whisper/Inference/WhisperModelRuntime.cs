using System.Diagnostics;
using System.Text.Json;
using ElBruno.HuggingFace;
using ElBruno.Whisper.Tokenizer;

namespace ElBruno.Whisper.Inference;

internal sealed class WhisperModelRuntime : IDisposable
{
    private readonly WhisperSessionPool _sessionPool;
    private bool _disposed;

    private WhisperModelRuntime(
        string modelId,
        WhisperTokenizer tokenizer,
        int[] configSuppressTokens,
        int[] beginSuppressTokens,
        WhisperSessionPool sessionPool)
    {
        ModelId = modelId;
        Tokenizer = tokenizer;
        ConfigSuppressTokens = configSuppressTokens;
        BeginSuppressTokens = beginSuppressTokens;
        _sessionPool = sessionPool;
    }

    public string ModelId { get; }

    public WhisperTokenizer Tokenizer { get; }

    public int[] ConfigSuppressTokens { get; }

    public int[] BeginSuppressTokens { get; }

    public bool SessionPoolingEnabled => _sessionPool.SessionPoolingEnabled;

    public int MaximumConcurrentRequests => _sessionPool.MaximumConcurrentRequests;

    internal static async Task<WhisperModelRuntime> CreateAsync(
        WhisperOptions options,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var modelPath = await ResolveModelPathAsync(options, progress, cancellationToken)
            .ConfigureAwait(false);

        var tokenizerPath = Path.Combine(modelPath, "tokenizer.json");
        var tokenizer = new WhisperTokenizer(tokenizerPath);
        var (configSuppressTokens, beginSuppressTokens) = LoadSuppressConfig(modelPath);

        var encoderPath = Path.Combine(modelPath, "onnx", "encoder_model.onnx");
        var decoderPath = Path.Combine(modelPath, "onnx", "decoder_model_merged.onnx");

        var sessionPool = new WhisperSessionPool(
            options.Model.Id,
            options.Concurrency,
            () => new WhisperInferenceSession(
                encoderPath,
                decoderPath,
                options.Model.NumDecoderLayers,
                options.Model.EncoderDimension));

        return new WhisperModelRuntime(
            options.Model.Id,
            tokenizer,
            configSuppressTokens,
            beginSuppressTokens,
            sessionPool);
    }

    internal async Task<int[]> RunInferenceAsync(
        float[] melSpectrogram,
        int[] initialTokens,
        int maxTokens,
        int eotToken,
        int[]? suppressTokens,
        int[]? beginSuppressTokens,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        using var lease = await _sessionPool.AcquireAsync(cancellationToken).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        var outcome = "success";

        try
        {
            return lease.Session.Inference(
                melSpectrogram,
                initialTokens,
                maxTokens,
                eotToken,
                suppressTokens,
                beginSuppressTokens,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            throw;
        }
        catch
        {
            outcome = "faulted";
            throw;
        }
        finally
        {
            stopwatch.Stop();
            WhisperMetrics.RecordInferenceDuration(
                ModelId,
                SessionPoolingEnabled,
                MaximumConcurrentRequests,
                stopwatch.Elapsed,
                outcome);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sessionPool.Dispose();
    }

    private static async Task<string> ResolveModelPathAsync(
        WhisperOptions options,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(options.ModelPath))
        {
            return options.ModelPath;
        }

        var cacheDir = options.CacheDirectory ?? DefaultPathHelper.GetDefaultCacheDirectory("ElBruno/Whisper");
        var modelPath = Path.Combine(cacheDir, options.Model.Id);

        if (options.EnsureModelDownloaded)
        {
            await DownloadModelAsync(options.Model, cacheDir, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        return modelPath;
    }

    private static (int[] SuppressTokens, int[] BeginSuppressTokens) LoadSuppressConfig(string modelPath)
    {
        var configPath = Path.Combine(modelPath, "config.json");
        if (!File.Exists(configPath))
        {
            return (Array.Empty<int>(), [220, 50256]);
        }

        var json = File.ReadAllText(configPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var suppressTokens = Array.Empty<int>();
        if (root.TryGetProperty("suppress_tokens", out var suppressProp))
        {
            suppressTokens = suppressProp.EnumerateArray()
                .Select(static element => element.GetInt32())
                .ToArray();
        }

        var beginSuppressTokens = new[] { 220, 50256 };
        if (root.TryGetProperty("begin_suppress_tokens", out var beginProp))
        {
            beginSuppressTokens = beginProp.EnumerateArray()
                .Select(static element => element.GetInt32())
                .ToArray();
        }

        return (suppressTokens, beginSuppressTokens);
    }

    private static async Task DownloadModelAsync(
        WhisperModelDefinition model,
        string cacheDir,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var modelDir = Path.Combine(cacheDir, model.Id);
        var allFilesExist = model.RequiredFiles.All(file => File.Exists(Path.Combine(modelDir, file)));
        if (allFilesExist)
        {
            return;
        }

        using var downloader = new HuggingFaceDownloader();
        var request = new DownloadRequest
        {
            RepoId = model.HuggingFaceRepoId,
            LocalDirectory = modelDir,
            RequiredFiles = model.RequiredFiles,
            OptionalFiles = model.OptionalFiles,
            Progress = progress
        };

        await downloader.DownloadFilesAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(WhisperModelRuntime));
    }
}
