using ElBruno.Whisper.BlazorComponents.Models;

namespace ElBruno.Whisper.BlazorComponents.Services;

public interface IWhisperService
{
    event EventHandler<LiveTranscriptUpdate>? TranscriptUpdated;

    Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        string? modelId = null,
        CancellationToken cancellationToken = default);

    Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string? modelId = null,
        CancellationToken cancellationToken = default);
}
