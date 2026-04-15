namespace ElBruno.Whisper;

/// <summary>
/// A timestamped segment of transcribed text.
/// </summary>
public sealed record TranscriptionSegment
{
    /// <summary>
    /// Start time of the segment in the audio.
    /// </summary>
    public required TimeSpan Start { get; init; }

    /// <summary>
    /// End time of the segment in the audio.
    /// </summary>
    public required TimeSpan End { get; init; }

    /// <summary>
    /// The transcribed text for this segment.
    /// </summary>
    public required string Text { get; init; }
}
