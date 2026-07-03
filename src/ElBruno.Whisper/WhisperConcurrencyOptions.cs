namespace ElBruno.Whisper;

/// <summary>
/// Controls how a <see cref="WhisperClient"/> handles concurrent transcription requests.
/// </summary>
public sealed class WhisperConcurrencyOptions
{
    private int _maximumConcurrentRequests = 1;
    private TimeSpan _queueTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of transcription requests that may execute at the same time.
    /// Defaults to 1, which preserves the original single-request behavior.
    /// </summary>
    public int MaximumConcurrentRequests
    {
        get => _maximumConcurrentRequests;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maximumConcurrentRequests = value;
        }
    }

    /// <summary>
    /// Maximum time a request may wait for an inference slot.
    /// Set to <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan QueueTimeout
    {
        get => _queueTimeout;
        set
        {
            if (value < TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    "Queue timeout must be non-negative or Timeout.InfiniteTimeSpan.");
            }

            _queueTimeout = value;
        }
    }

    /// <summary>
    /// Reuses inference sessions between requests when true. Disable this to create
    /// a fresh ONNX session per request while still honoring the concurrency limit.
    /// Defaults to true.
    /// </summary>
    public bool EnableSessionPooling { get; set; } = true;
}
