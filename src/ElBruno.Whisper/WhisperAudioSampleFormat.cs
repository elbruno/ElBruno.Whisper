namespace ElBruno.Whisper;

/// <summary>
/// Supported raw audio sample encodings for explicit-audio transcription APIs.
/// </summary>
public enum WhisperAudioSampleFormat
{
    /// <summary>
    /// Signed 16-bit little-endian PCM.
    /// </summary>
    Pcm16 = 0,

    /// <summary>
    /// 32-bit IEEE floating point little-endian PCM.
    /// </summary>
    Float32 = 1
}
