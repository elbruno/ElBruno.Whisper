using ElBruno.Whisper.BlazorComponents.Models;

namespace ElBruno.Whisper.BlazorComponents.Services;

public interface IWhisperModelManager
{
    Task<IReadOnlyList<WhisperModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);
    Task DownloadModelAsync(string modelId, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
