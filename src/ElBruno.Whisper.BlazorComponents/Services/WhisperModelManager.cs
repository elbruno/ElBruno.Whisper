using System.Collections.Concurrent;
using ElBruno.HuggingFace;
using ElBruno.Whisper.BlazorComponents.Models;

namespace ElBruno.Whisper.BlazorComponents.Services;

public sealed class WhisperModelManager(WhisperOptions options) : IWhisperModelManager
{
    private readonly ConcurrentDictionary<string, double> _downloadProgress = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<WhisperModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var cacheDirectory = options.CacheDirectory ?? GetDefaultCacheDirectory();
        var models = KnownWhisperModels.All
            .Select(model =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var modelPath = Path.Combine(cacheDirectory, model.Id);
                var hasRequiredFiles = model.RequiredFiles.All(file => File.Exists(Path.Combine(modelPath, file)));
                var progress = _downloadProgress.GetValueOrDefault(model.Id);
                var state = progress > 0 && progress < 100
                    ? WhisperModelState.Downloading
                    : hasRequiredFiles
                        ? WhisperModelState.Downloaded
                        : WhisperModelState.NotDownloaded;

                return new WhisperModelInfo
                {
                    Id = model.Id,
                    DisplayName = model.DisplayName,
                    Size = model.Size,
                    State = state,
                    DownloadPercent = progress
                };
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<WhisperModelInfo>>(models);
    }

    public async Task DownloadModelAsync(string modelId, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var model = KnownWhisperModels.FindById(modelId)
                    ?? throw new ArgumentException($"Unknown model id '{modelId}'.", nameof(modelId));

        var downloadProgress = new Progress<DownloadProgress>(p =>
        {
            _downloadProgress[model.Id] = p.PercentComplete;
            progress?.Report(p.PercentComplete);
        });

        var modelOptions = new WhisperOptions
        {
            Model = model,
            CacheDirectory = options.CacheDirectory,
            EnsureModelDownloaded = true
        };

        using var client = await WhisperClient.CreateAsync(modelOptions, downloadProgress, cancellationToken);
        _downloadProgress[model.Id] = 100;
    }

    private static string GetDefaultCacheDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        var cacheDir = Path.Combine(localAppData, "ElBruno", "Whisper", "models");
        Directory.CreateDirectory(cacheDir);
        return cacheDir;
    }
}
