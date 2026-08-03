using ElBruno.Whisper.BlazorComponents.Models;

namespace ElBruno.Whisper.BlazorComponents.Services;

public sealed class WhisperService(WhisperOptions options) : IWhisperService
{
    public event EventHandler<LiveTranscriptUpdate>? TranscriptUpdated;

    public async Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        var selectedModel = ResolveModel(modelId);
        using var client = await WhisperClient.CreateAsync(
            new WhisperOptions
            {
                Model = selectedModel,
                CacheDirectory = options.CacheDirectory,
                EnsureModelDownloaded = options.EnsureModelDownloaded,
                EnableTimestamps = true,
                Language = options.Language,
                Translate = options.Translate,
                MaxTokens = options.MaxTokens,
                Temperature = options.Temperature
            },
            cancellationToken: cancellationToken);

        var result = await client.TranscribeAsync(audioFilePath, cancellationToken);
        PublishFinalUpdate(result);
        return result;
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        var selectedModel = ResolveModel(modelId);
        using var client = await WhisperClient.CreateAsync(
            new WhisperOptions
            {
                Model = selectedModel,
                CacheDirectory = options.CacheDirectory,
                EnsureModelDownloaded = options.EnsureModelDownloaded,
                EnableTimestamps = true,
                Language = options.Language,
                Translate = options.Translate,
                MaxTokens = options.MaxTokens,
                Temperature = options.Temperature
            },
            cancellationToken: cancellationToken);

        var result = await client.TranscribeAsync(audioStream, cancellationToken);
        PublishFinalUpdate(result);
        return result;
    }

    private WhisperModelDefinition ResolveModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return options.Model;
        }

        return KnownWhisperModels.FindById(modelId)
               ?? throw new ArgumentException($"Unknown model id '{modelId}'.", nameof(modelId));
    }

    private void PublishFinalUpdate(TranscriptionResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Text))
        {
            return;
        }

        TranscriptUpdated?.Invoke(this, new LiveTranscriptUpdate
        {
            Text = result.Text.Trim(),
            Timestamp = DateTimeOffset.UtcNow,
            IsFinal = true,
            Language = result.DetectedLanguage
        });
    }
}
