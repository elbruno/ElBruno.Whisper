namespace ElBruno.Whisper;

/// <summary>
/// Describes raw PCM audio so the client can normalize it into Whisper's 16 kHz mono input format.
/// </summary>
public readonly record struct WhisperAudioFormat
{
    /// <summary>
    /// Creates a raw audio format descriptor.
    /// </summary>
    public WhisperAudioFormat(int sampleRate, int channels, WhisperAudioSampleFormat sampleFormat)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        if (!Enum.IsDefined(sampleFormat))
        {
            throw new ArgumentOutOfRangeException(nameof(sampleFormat), sampleFormat, "Unsupported sample format.");
        }

        SampleRate = sampleRate;
        Channels = channels;
        SampleFormat = sampleFormat;
    }

    /// <summary>
    /// Source sample rate in Hz.
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Source channel count.
    /// </summary>
    public int Channels { get; }

    /// <summary>
    /// Raw sample encoding.
    /// </summary>
    public WhisperAudioSampleFormat SampleFormat { get; }

    internal int BytesPerSample =>
        SampleFormat switch
        {
            WhisperAudioSampleFormat.Pcm16 => sizeof(short),
            WhisperAudioSampleFormat.Float32 => sizeof(float),
            _ => throw new ArgumentOutOfRangeException(nameof(SampleFormat), SampleFormat, "Unsupported sample format.")
        };

    internal int BytesPerFrame => checked(BytesPerSample * Channels);
}
