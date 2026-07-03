namespace ElBruno.Whisper;

/// <summary>
/// A timestamped word in the transcription output.
/// </summary>
public sealed record TranscriptionWord
{
    /// <summary>
    /// Start time of the word in the audio.
    /// </summary>
    public required TimeSpan Start { get; init; }

    /// <summary>
    /// End time of the word in the audio.
    /// </summary>
    public required TimeSpan End { get; init; }

    /// <summary>
    /// The transcribed word or token group.
    /// </summary>
    public required string Text { get; init; }
}
