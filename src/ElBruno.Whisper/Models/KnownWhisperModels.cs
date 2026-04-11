namespace ElBruno.Whisper;

/// <summary>
/// Pre-defined Whisper ONNX models from the onnx-community on HuggingFace.
/// </summary>
public static class KnownWhisperModels
{
    private static readonly string[] StandardRequiredFiles =
    [
        "onnx/encoder_model.onnx",
        "onnx/decoder_model.onnx",
        "onnx/decoder_model_merged.onnx",
        "config.json",
        "tokenizer.json",
        "preprocessor_config.json",
        "generation_config.json"
    ];

    /// <summary>
    /// Whisper Tiny English-only model (~75MB) - fastest, best for quick start.
    /// </summary>
    public static WhisperModelDefinition WhisperTinyEn { get; } = new()
    {
        Id = "whisper-tiny.en",
        DisplayName = "Whisper Tiny (English)",
        HuggingFaceRepoId = "onnx-community/whisper-tiny.en",
        RequiredFiles = StandardRequiredFiles,
        Size = WhisperModelSize.Tiny,
        IsEnglishOnly = true,
        IsMultilingual = false,
        EncoderDimension = 384,
        NumDecoderLayers = 4
    };

    /// <summary>
    /// Whisper Tiny multilingual model (~150MB).
    /// </summary>
    public static WhisperModelDefinition WhisperTiny { get; } = new()
    {
        Id = "whisper-tiny",
        DisplayName = "Whisper Tiny (Multilingual)",
        HuggingFaceRepoId = "onnx-community/whisper-tiny",
        RequiredFiles = StandardRequiredFiles,
        Size = WhisperModelSize.Tiny,
        IsEnglishOnly = false,
        IsMultilingual = true,
        EncoderDimension = 384,
        NumDecoderLayers = 4
    };

    /// <summary>
    /// Whisper Base English-only model (~140MB).
    /// </summary>
    public static WhisperModelDefinition WhisperBaseEn { get; } = new()
    {
        Id = "whisper-base.en",
        DisplayName = "Whisper Base (English)",
        HuggingFaceRepoId = "onnx-community/whisper-base.en",
        RequiredFiles = StandardRequiredFiles,
        Size = WhisperModelSize.Base,
        IsEnglishOnly = true,
        IsMultilingual = false,
        EncoderDimension = 512,
        NumDecoderLayers = 6
    };

    /// <summary>
    /// Whisper Base multilingual model (~290MB).
    /// </summary>
    public static WhisperModelDefinition WhisperBase { get; } = new()
    {
        Id = "whisper-base",
        DisplayName = "Whisper Base (Multilingual)",
        HuggingFaceRepoId = "onnx-community/whisper-base",
        RequiredFiles = StandardRequiredFiles,
        Size = WhisperModelSize.Base,
        IsEnglishOnly = false,
        IsMultilingual = true,
        EncoderDimension = 512,
        NumDecoderLayers = 6
    };

    /// <summary>
    /// Whisper Small English-only model (~250MB).
    /// </summary>
    public static WhisperModelDefinition WhisperSmallEn { get; } = new()
    {
        Id = "whisper-small.en",
        DisplayName = "Whisper Small (English)",
        HuggingFaceRepoId = "onnx-community/whisper-small.en",
        RequiredFiles = StandardRequiredFiles,
        Size = WhisperModelSize.Small,
        IsEnglishOnly = true,
        IsMultilingual = false,
        EncoderDimension = 768,
        NumDecoderLayers = 12
    };

    /// <summary>
    /// Whisper Small multilingual model (~490MB).
    /// </summary>
    public static WhisperModelDefinition WhisperSmall { get; } = new()
    {
        Id = "whisper-small",
        DisplayName = "Whisper Small (Multilingual)",
        HuggingFaceRepoId = "onnx-community/whisper-small",
        RequiredFiles = StandardRequiredFiles,
        Size = WhisperModelSize.Small,
        IsEnglishOnly = false,
        IsMultilingual = true,
        EncoderDimension = 768,
        NumDecoderLayers = 12
    };

    /// <summary>
    /// Whisper Medium English-only model (~500MB).
    /// </summary>
    public static WhisperModelDefinition WhisperMediumEn { get; } = new()
    {
        Id = "whisper-medium.en",
        DisplayName = "Whisper Medium (English)",
        HuggingFaceRepoId = "onnx-community/whisper-medium.en",
        RequiredFiles = StandardRequiredFiles,
        Size = WhisperModelSize.Medium,
        IsEnglishOnly = true,
        IsMultilingual = false,
        EncoderDimension = 1024,
        NumDecoderLayers = 24
    };

    /// <summary>
    /// Whisper Medium multilingual model (~1.1GB).
    /// </summary>
    public static WhisperModelDefinition WhisperMedium { get; } = new()
    {
        Id = "whisper-medium",
        DisplayName = "Whisper Medium (Multilingual)",
        HuggingFaceRepoId = "onnx-community/whisper-medium",
        RequiredFiles = StandardRequiredFiles,
        Size = WhisperModelSize.Medium,
        IsEnglishOnly = false,
        IsMultilingual = true,
        EncoderDimension = 1024,
        NumDecoderLayers = 24
    };

    /// <summary>
    /// Whisper Large v3 multilingual model (~1.5GB) - highest accuracy.
    /// </summary>
    public static WhisperModelDefinition WhisperLargeV3 { get; } = new()
    {
        Id = "whisper-large-v3",
        DisplayName = "Whisper Large v3 (Multilingual)",
        HuggingFaceRepoId = "onnx-community/whisper-large-v3",
        RequiredFiles = StandardRequiredFiles,
        Size = WhisperModelSize.Large,
        IsEnglishOnly = false,
        IsMultilingual = true,
        EncoderDimension = 1280,
        NumDecoderLayers = 32
    };

    /// <summary>
    /// Whisper Large v3 Turbo multilingual model (~646MB) - optimized large model.
    /// </summary>
    public static WhisperModelDefinition WhisperLargeV3Turbo { get; } = new()
    {
        Id = "whisper-large-v3-turbo",
        DisplayName = "Whisper Large v3 Turbo (Multilingual)",
        HuggingFaceRepoId = "onnx-community/whisper-large-v3-turbo",
        RequiredFiles = StandardRequiredFiles,
        Size = WhisperModelSize.LargeTurbo,
        IsEnglishOnly = false,
        IsMultilingual = true,
        EncoderDimension = 1280,
        NumDecoderLayers = 4
    };

    /// <summary>
    /// All available Whisper models.
    /// </summary>
    public static IReadOnlyList<WhisperModelDefinition> All { get; } = new[]
    {
        WhisperTinyEn,
        WhisperTiny,
        WhisperBaseEn,
        WhisperBase,
        WhisperSmallEn,
        WhisperSmall,
        WhisperMediumEn,
        WhisperMedium,
        WhisperLargeV3,
        WhisperLargeV3Turbo
    };

    /// <summary>
    /// Finds a model by its ID.
    /// </summary>
    /// <param name="id">The model ID to search for.</param>
    /// <returns>The matching model definition, or null if not found.</returns>
    public static WhisperModelDefinition? FindById(string id)
    {
        return All.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }
}
