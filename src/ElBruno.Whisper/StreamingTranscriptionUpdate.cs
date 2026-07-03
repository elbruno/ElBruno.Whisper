namespace ElBruno.Whisper;

/// <summary>
/// Incremental update emitted by <see cref="WhisperClient.GetStreamingTextAsync(string, WhisperStreamingOptions?, CancellationToken)"/>.
/// </summary>
public sealed record StreamingTranscriptionUpdate
{
    /// <summary>
    /// Combined transcript text for the current update.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Text that is considered stable and will not be revised by later updates.
    /// </summary>
    public required string CommittedText { get; init; }

    /// <summary>
    /// Most recent rolling hypothesis that may still change in later updates.
    /// </summary>
    public required string ProvisionalText { get; init; }

    /// <summary>
    /// Start offset of the rolling window that produced this update.
    /// </summary>
    public required TimeSpan WindowStart { get; init; }

    /// <summary>
    /// End offset of the rolling window that produced this update.
    /// </summary>
    public required TimeSpan WindowEnd { get; init; }

    /// <summary>
    /// Total audio duration represented by the source being transcribed.
    /// </summary>
    public required TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// True only for the final successful update, which flushes all remaining provisional text.
    /// </summary>
    public bool IsFinal { get; init; }
}
