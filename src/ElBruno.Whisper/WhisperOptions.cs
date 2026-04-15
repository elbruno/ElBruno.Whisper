namespace ElBruno.Whisper;

/// <summary>
/// Configuration options for WhisperClient.
/// </summary>
public sealed class WhisperOptions
{
    /// <summary>
    /// The Whisper model to use. Defaults to WhisperTinyEn for quick start.
    /// </summary>
    public WhisperModelDefinition Model { get; set; } = KnownWhisperModels.WhisperTinyEn;

    /// <summary>
    /// Optional path to a local model directory. If specified, model download is skipped.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Directory for caching downloaded models. Defaults to %LOCALAPPDATA%/ElBruno/Whisper/models.
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// If true, automatically download the model if not present. Defaults to true.
    /// </summary>
    public bool EnsureModelDownloaded { get; set; } = true;

    /// <summary>
    /// Language code (e.g., "en", "es", "fr"). Null for auto-detection.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// If true, translate the audio to English. Defaults to false (transcribe only).
    /// </summary>
    public bool Translate { get; set; }

    /// <summary>
    /// Maximum number of tokens to generate. Defaults to 448.
    /// </summary>
    public int MaxTokens { get; set; } = 448;

    /// <summary>
    /// Sampling temperature. 0.0 = greedy (deterministic), higher = more random.
    /// </summary>
    public float Temperature { get; set; } = 0.0f;

    /// <summary>
    /// If true, extract timestamp information from the model output.
    /// Results will include <see cref="TranscriptionResult.Segments"/> with start/end times.
    /// Defaults to false for backward compatibility.
    /// </summary>
    public bool EnableTimestamps { get; set; }
}
