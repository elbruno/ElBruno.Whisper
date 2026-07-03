namespace ElBruno.Whisper;

internal interface IWhisperTranscriptionBackend : IDisposable
{
    Task<TranscriptionResult> TranscribeAsync(
        float[] melSpectrogram,
        TimeSpan audioDuration,
        CancellationToken cancellationToken);
}
