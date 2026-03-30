namespace ElBruno.Whisper;

/// <summary>
/// Result of a transcription operation.
/// </summary>
public sealed record TranscriptionResult
{
    /// <summary>
    /// The transcribed or translated text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Detected language code (e.g., "en", "es"), if available.
    /// </summary>
    public string? DetectedLanguage { get; init; }

    /// <summary>
    /// Duration of the audio processed.
    /// </summary>
    public TimeSpan Duration { get; init; }
}
