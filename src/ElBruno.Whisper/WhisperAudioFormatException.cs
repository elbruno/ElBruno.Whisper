namespace ElBruno.Whisper;

/// <summary>
/// Thrown when audio bytes cannot be interpreted as supported WAV or raw PCM input.
/// </summary>
public sealed class WhisperAudioFormatException : FormatException
{
    /// <summary>
    /// Creates a new <see cref="WhisperAudioFormatException"/>.
    /// </summary>
    public WhisperAudioFormatException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new <see cref="WhisperAudioFormatException"/> with an inner exception.
    /// </summary>
    public WhisperAudioFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
