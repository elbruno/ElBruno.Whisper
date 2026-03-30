namespace ElBruno.Whisper;

/// <summary>
/// Represents the size categories for Whisper models.
/// </summary>
public enum WhisperModelSize
{
    /// <summary>
    /// Tiny model (~75-150MB) - fastest, lowest accuracy
    /// </summary>
    Tiny,
    
    /// <summary>
    /// Base model (~140-290MB) - good balance of speed and accuracy
    /// </summary>
    Base,
    
    /// <summary>
    /// Small model (~250-490MB) - improved accuracy
    /// </summary>
    Small,
    
    /// <summary>
    /// Medium model (~500MB-1.1GB) - high accuracy
    /// </summary>
    Medium,
    
    /// <summary>
    /// Large model (~1.5GB) - highest accuracy
    /// </summary>
    Large,
    
    /// <summary>
    /// Large Turbo model (~646MB) - optimized large model with fewer decoder layers
    /// </summary>
    LargeTurbo
}
