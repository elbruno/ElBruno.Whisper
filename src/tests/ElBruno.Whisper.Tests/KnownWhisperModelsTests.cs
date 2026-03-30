using Xunit;

namespace ElBruno.Whisper.Tests;

public class KnownWhisperModelsTests
{
    [Fact]
    public void AllPredefinedModels_AreNotNull()
    {
        Assert.NotNull(KnownWhisperModels.WhisperTinyEn);
        Assert.NotNull(KnownWhisperModels.WhisperTiny);
        Assert.NotNull(KnownWhisperModels.WhisperBaseEn);
        Assert.NotNull(KnownWhisperModels.WhisperBase);
        Assert.NotNull(KnownWhisperModels.WhisperSmallEn);
        Assert.NotNull(KnownWhisperModels.WhisperSmall);
        Assert.NotNull(KnownWhisperModels.WhisperMediumEn);
        Assert.NotNull(KnownWhisperModels.WhisperMedium);
        Assert.NotNull(KnownWhisperModels.WhisperLargeV3);
        Assert.NotNull(KnownWhisperModels.WhisperLargeV3Turbo);
    }

    [Fact]
    public void AllModels_HaveUniqueIds()
    {
        var models = KnownWhisperModels.All;
        var ids = models.Select(m => m.Id).ToList();
        var uniqueIds = ids.Distinct().ToList();

        Assert.Equal(ids.Count, uniqueIds.Count);
    }

    [Fact]
    public void AllModels_HaveValidHuggingFaceRepoId()
    {
        var models = KnownWhisperModels.All;

        foreach (var model in models)
        {
            Assert.NotNull(model.HuggingFaceRepoId);
            Assert.NotEmpty(model.HuggingFaceRepoId);
        }
    }

    [Fact]
    public void AllModels_HaveRequiredFiles()
    {
        var models = KnownWhisperModels.All;

        foreach (var model in models)
        {
            Assert.NotNull(model.RequiredFiles);
            Assert.NotEmpty(model.RequiredFiles);
        }
    }

    [Fact]
    public void FindById_ReturnsCorrectModel()
    {
        var model = KnownWhisperModels.FindById("whisper-tiny.en");

        Assert.NotNull(model);
        Assert.Equal("whisper-tiny.en", model.Id);
    }

    [Fact]
    public void FindById_ReturnsNullForUnknownId()
    {
        var model = KnownWhisperModels.FindById("unknown-model");

        Assert.Null(model);
    }

    [Fact]
    public void EnglishOnlyModels_AreMarkedCorrectly()
    {
        var englishOnlyModels = new[]
        {
            KnownWhisperModels.WhisperTinyEn,
            KnownWhisperModels.WhisperBaseEn,
            KnownWhisperModels.WhisperSmallEn,
            KnownWhisperModels.WhisperMediumEn
        };

        foreach (var model in englishOnlyModels)
        {
            Assert.True(model.IsEnglishOnly, $"{model.Id} should be English-only");
            Assert.False(model.IsMultilingual, $"{model.Id} should not be multilingual");
        }
    }

    [Fact]
    public void MultilingualModels_AreMarkedCorrectly()
    {
        var multilingualModels = new[]
        {
            KnownWhisperModels.WhisperTiny,
            KnownWhisperModels.WhisperBase,
            KnownWhisperModels.WhisperSmall,
            KnownWhisperModels.WhisperMedium,
            KnownWhisperModels.WhisperLargeV3,
            KnownWhisperModels.WhisperLargeV3Turbo
        };

        foreach (var model in multilingualModels)
        {
            Assert.True(model.IsMultilingual, $"{model.Id} should be multilingual");
            Assert.False(model.IsEnglishOnly, $"{model.Id} should not be English-only");
        }
    }

    [Theory]
    [InlineData("whisper-tiny.en", WhisperModelSize.Tiny)]
    [InlineData("whisper-tiny", WhisperModelSize.Tiny)]
    [InlineData("whisper-base.en", WhisperModelSize.Base)]
    [InlineData("whisper-base", WhisperModelSize.Base)]
    [InlineData("whisper-small.en", WhisperModelSize.Small)]
    [InlineData("whisper-small", WhisperModelSize.Small)]
    [InlineData("whisper-medium.en", WhisperModelSize.Medium)]
    [InlineData("whisper-medium", WhisperModelSize.Medium)]
    [InlineData("whisper-large-v3", WhisperModelSize.Large)]
    [InlineData("whisper-large-v3-turbo", WhisperModelSize.LargeTurbo)]
    public void Models_HaveCorrectSize(string modelId, WhisperModelSize expectedSize)
    {
        var model = KnownWhisperModels.FindById(modelId);

        Assert.NotNull(model);
        Assert.Equal(expectedSize, model.Size);
    }

    [Fact]
    public void AllCollection_ContainsAllExpectedModels()
    {
        var models = KnownWhisperModels.All;

        Assert.Contains(models, m => m.Id == "whisper-tiny.en");
        Assert.Contains(models, m => m.Id == "whisper-tiny");
        Assert.Contains(models, m => m.Id == "whisper-base.en");
        Assert.Contains(models, m => m.Id == "whisper-base");
        Assert.Contains(models, m => m.Id == "whisper-small.en");
        Assert.Contains(models, m => m.Id == "whisper-small");
        Assert.Contains(models, m => m.Id == "whisper-medium.en");
        Assert.Contains(models, m => m.Id == "whisper-medium");
        Assert.Contains(models, m => m.Id == "whisper-large-v3");
        Assert.Contains(models, m => m.Id == "whisper-large-v3-turbo");
    }

    [Fact]
    public void DefaultModel_ExistsInAll()
    {
        var models = KnownWhisperModels.All;
        var defaultModel = KnownWhisperModels.WhisperTinyEn;

        Assert.Contains(defaultModel, models);
    }

    [Fact]
    public void AllModels_HaveDisplayName()
    {
        var models = KnownWhisperModels.All;

        foreach (var model in models)
        {
            Assert.NotNull(model.DisplayName);
            Assert.NotEmpty(model.DisplayName);
        }
    }
}
