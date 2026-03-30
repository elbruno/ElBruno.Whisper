using Xunit;

namespace ElBruno.Whisper.Tests;

public class WhisperModelDefinitionTests
{
    [Fact]
    public void WhisperModelDefinition_HasRequiredProperties()
    {
        var model = new WhisperModelDefinition
        {
            Id = "test-model",
            DisplayName = "Test Model",
            HuggingFaceRepoId = "test/repo",
            RequiredFiles = new[] { "model.onnx", "config.json" },
            Size = WhisperModelSize.Tiny
        };

        Assert.Equal("test-model", model.Id);
        Assert.Equal("Test Model", model.DisplayName);
        Assert.Equal("test/repo", model.HuggingFaceRepoId);
        Assert.Equal(2, model.RequiredFiles.Length);
        Assert.Equal(WhisperModelSize.Tiny, model.Size);
    }

    [Fact]
    public void DefaultIsMultilingual_ShouldBeTrue()
    {
        var model = new WhisperModelDefinition
        {
            Id = "test-model",
            DisplayName = "Test Model",
            HuggingFaceRepoId = "test/repo",
            RequiredFiles = new[] { "model.onnx" },
            Size = WhisperModelSize.Tiny
        };

        Assert.True(model.IsMultilingual);
    }

    [Fact]
    public void EnglishOnlyModels_HaveCorrectProperties()
    {
        var model = new WhisperModelDefinition
        {
            Id = "whisper-tiny-en",
            DisplayName = "Whisper Tiny English",
            HuggingFaceRepoId = "openai/whisper-tiny.en",
            RequiredFiles = new[] { "model.onnx" },
            Size = WhisperModelSize.Tiny,
            IsEnglishOnly = true,
            IsMultilingual = false
        };

        Assert.True(model.IsEnglishOnly);
        Assert.False(model.IsMultilingual);
    }

    [Fact]
    public void RequiredFiles_CanContainMultipleFiles()
    {
        var files = new[] { "model.onnx", "encoder.onnx", "decoder.onnx", "config.json", "vocab.txt" };
        var model = new WhisperModelDefinition
        {
            Id = "test-model",
            DisplayName = "Test Model",
            HuggingFaceRepoId = "test/repo",
            RequiredFiles = files,
            Size = WhisperModelSize.Medium
        };

        Assert.Equal(5, model.RequiredFiles.Length);
        Assert.Contains("model.onnx", model.RequiredFiles);
        Assert.Contains("config.json", model.RequiredFiles);
    }

    [Theory]
    [InlineData(WhisperModelSize.Tiny)]
    [InlineData(WhisperModelSize.Base)]
    [InlineData(WhisperModelSize.Small)]
    [InlineData(WhisperModelSize.Medium)]
    [InlineData(WhisperModelSize.Large)]
    public void CanSetAllModelSizes(WhisperModelSize size)
    {
        var model = new WhisperModelDefinition
        {
            Id = "test-model",
            DisplayName = "Test Model",
            HuggingFaceRepoId = "test/repo",
            RequiredFiles = new[] { "model.onnx" },
            Size = size
        };

        Assert.Equal(size, model.Size);
    }
}
