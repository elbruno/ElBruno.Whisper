namespace ElBruno.Whisper.Inference;

internal interface IWhisperInferenceSession : IDisposable
{
    int[] Inference(
        float[] melSpectrogram,
        int[] initialTokens,
        int maxTokens,
        int eotToken,
        int[]? suppressTokens = null,
        int[]? beginSuppressTokens = null,
        CancellationToken cancellationToken = default);
}
