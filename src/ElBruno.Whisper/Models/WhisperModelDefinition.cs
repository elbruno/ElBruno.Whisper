namespace ElBruno.Whisper;

/// <summary>
/// Defines a Whisper model configuration including HuggingFace repository and model specifications.
/// </summary>
public sealed record WhisperModelDefinition
{
    /// <summary>
    /// Unique identifier for the model.
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string DisplayName { get; init; }
    
    /// <summary>
    /// HuggingFace repository ID (e.g., "onnx-community/whisper-tiny.en").
    /// </summary>
    public required string HuggingFaceRepoId { get; init; }
    
    /// <summary>
    /// Files that must be downloaded for the model to work.
    /// </summary>
    public required string[] RequiredFiles { get; init; }
    
    /// <summary>
    /// Optional files that may enhance model functionality.
    /// </summary>
    public string[] OptionalFiles { get; init; } = [];
    
    /// <summary>
    /// Model size category.
    /// </summary>
    public required WhisperModelSize Size { get; init; }
    
    /// <summary>
    /// True if the model is English-only.
    /// </summary>
    public bool IsEnglishOnly { get; init; }
    
    /// <summary>
    /// True if the model supports multiple languages.
    /// </summary>
    public bool IsMultilingual { get; init; } = true;
    
    /// <summary>
    /// Encoder hidden dimension size.
    /// </summary>
    public int EncoderDimension { get; init; } = 384;
    
    /// <summary>
    /// Number of decoder layers.
    /// </summary>
    public int NumDecoderLayers { get; init; } = 4;
}
