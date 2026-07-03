namespace ElBruno.Whisper;

/// <summary>
/// Controls rolling-window transcription updates returned by <see cref="WhisperClient.GetStreamingTextAsync(string, WhisperStreamingOptions?, CancellationToken)"/>.
/// </summary>
public sealed class WhisperStreamingOptions
{
    /// <summary>
    /// Amount of new audio to analyze on each transcription pass.
    /// </summary>
    public TimeSpan WindowSize { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// How far the rolling cursor advances between updates.
    /// </summary>
    public TimeSpan StepSize { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Audio preserved from the prior cursor position to give the next pass short-term context.
    /// </summary>
    public TimeSpan ContextOverlap { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// When enabled, committed text waits for agreement across successive rolling hypotheses.
    /// </summary>
    public bool UseLocalAgreement { get; set; } = true;

    /// <summary>
    /// Number of successive hypotheses required before the oldest hypothesis is committed.
    /// </summary>
    public int AgreementIterations { get; set; } = 2;

    internal void Validate()
    {
        if (WindowSize <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(WindowSize), WindowSize, "WindowSize must be greater than zero.");
        }

        if (StepSize <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(StepSize), StepSize, "StepSize must be greater than zero.");
        }

        if (ContextOverlap < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ContextOverlap), ContextOverlap, "ContextOverlap cannot be negative.");
        }

        if (ContextOverlap >= WindowSize)
        {
            throw new ArgumentOutOfRangeException(nameof(ContextOverlap), ContextOverlap, "ContextOverlap must be smaller than WindowSize.");
        }

        if (AgreementIterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(AgreementIterations), AgreementIterations, "AgreementIterations must be at least 1.");
        }
    }

    internal int GetRequiredHypothesisCount()
    {
        return UseLocalAgreement
            ? Math.Max(2, AgreementIterations)
            : 2;
    }
}
