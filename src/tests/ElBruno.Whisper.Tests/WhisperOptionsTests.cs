using Xunit;

namespace ElBruno.Whisper.Tests;

public class WhisperOptionsTests
{
    [Fact]
    public void DefaultModel_ShouldBeWhisperTinyEn()
    {
        var options = new WhisperOptions();
        
        Assert.NotNull(options.Model);
        Assert.Equal("whisper-tiny.en", options.Model.Id);
    }

    [Fact]
    public void DefaultEnsureModelDownloaded_ShouldBeTrue()
    {
        var options = new WhisperOptions();
        
        Assert.True(options.EnsureModelDownloaded);
    }

    [Fact]
    public void DefaultLanguage_ShouldBeNull()
    {
        var options = new WhisperOptions();
        
        Assert.Null(options.Language);
    }

    [Fact]
    public void DefaultTemperature_ShouldBeZero()
    {
        var options = new WhisperOptions();
        
        Assert.Equal(0.0f, options.Temperature);
    }

    [Fact]
    public void DefaultMaxTokens_ShouldBe448()
    {
        var options = new WhisperOptions();
        
        Assert.Equal(448, options.MaxTokens);
    }

    [Fact]
    public void DefaultTranslate_ShouldBeFalse()
    {
        var options = new WhisperOptions();
        
        Assert.False(options.Translate);
    }

    [Fact]
    public void CanSetCustomCacheDirectory()
    {
        var options = new WhisperOptions
        {
            CacheDirectory = @"C:\custom\cache"
        };
        
        Assert.Equal(@"C:\custom\cache", options.CacheDirectory);
    }

    [Fact]
    public void CanSetCustomModelPath()
    {
        var options = new WhisperOptions
        {
            ModelPath = @"C:\models\whisper.onnx"
        };
        
        Assert.Equal(@"C:\models\whisper.onnx", options.ModelPath);
    }

    [Fact]
    public void CanSetCustomModel()
    {
        var customModel = new WhisperModelDefinition
        {
            Id = "custom-model",
            DisplayName = "Custom Model",
            HuggingFaceRepoId = "openai/whisper-custom",
            RequiredFiles = new[] { "model.onnx" },
            Size = WhisperModelSize.Small
        };

        var options = new WhisperOptions
        {
            Model = customModel
        };
        
        Assert.Equal("custom-model", options.Model.Id);
    }

    [Fact]
    public void CanSetLanguage()
    {
        var options = new WhisperOptions
        {
            Language = "en"
        };
        
        Assert.Equal("en", options.Language);
    }

    [Fact]
    public void CanSetTemperature()
    {
        var options = new WhisperOptions
        {
            Temperature = 0.5f
        };
        
        Assert.Equal(0.5f, options.Temperature);
    }

    [Fact]
    public void CanSetMaxTokens()
    {
        var options = new WhisperOptions
        {
            MaxTokens = 224
        };
        
        Assert.Equal(224, options.MaxTokens);
    }

    [Fact]
    public void CanSetTranslate()
    {
        var options = new WhisperOptions
        {
            Translate = true
        };
        
        Assert.True(options.Translate);
    }

    [Fact]
    public void DefaultEnableTimestamps_ShouldBeFalse()
    {
        var options = new WhisperOptions();

        Assert.False(options.EnableTimestamps);
    }

    [Fact]
    public void CanSetEnableTimestamps()
    {
        var options = new WhisperOptions
        {
            EnableTimestamps = true
        };

        Assert.True(options.EnableTimestamps);
    }

    [Fact]
    public void EnableTimestamps_CanBeToggledBackToFalse()
    {
        var options = new WhisperOptions
        {
            EnableTimestamps = true
        };
        options.EnableTimestamps = false;

        Assert.False(options.EnableTimestamps);
    }
}
