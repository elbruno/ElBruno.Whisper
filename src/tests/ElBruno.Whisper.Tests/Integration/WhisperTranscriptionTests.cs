using Xunit;

namespace ElBruno.Whisper.Tests.Integration;

/// <summary>
/// Integration tests that download a real Whisper model and perform transcription.
/// These tests require network access and take significant time (model download + inference).
/// Exclude from CI with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class WhisperTranscriptionTests
{
    private const int TimeoutMs = 5 * 60 * 1000; // 5 minutes

    private static string GetTestDataPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
    }

    [Fact(Timeout = TimeoutMs)]
    public async Task TranscribeMediumAudio_ReturnsNonEmptyText()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioPath = GetTestDataPath("test-audio-medium.wav");
        var result = await client.TranscribeAsync(audioPath);

        Assert.NotNull(result);
        Assert.NotNull(result.Text);
        Assert.NotEmpty(result.Text);
    }

    [Fact(Timeout = TimeoutMs)]
    public async Task TranscribeSmallAudio_CompletesWithoutError()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioPath = GetTestDataPath("test-audio-small.wav");
        var result = await client.TranscribeAsync(audioPath);

        Assert.NotNull(result);
        Assert.NotNull(result.Text);
    }

    [Fact(Timeout = TimeoutMs)]
    public async Task TranscribeFailingAudio_CompletesWithoutError()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioPath = GetTestDataPath("test-audio-failing.wav");
        var result = await client.TranscribeAsync(audioPath);

        Assert.NotNull(result);
        Assert.NotNull(result.Text);
    }

    [Fact(Timeout = TimeoutMs)]
    public async Task TranscribeAsync_ThrowsFileNotFoundForMissingAudio()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var client = await WhisperClient.CreateAsync(options);

        var nonExistentPath = Path.Combine(Path.GetTempPath(), "does-not-exist.wav");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => client.TranscribeAsync(nonExistentPath));
    }

    [Fact(Timeout = TimeoutMs)]
    public async Task TranscribeStream_MediumAudio_ReturnsNonEmptyText()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioPath = GetTestDataPath("test-audio-medium.wav");
        using var stream = File.OpenRead(audioPath);
        var result = await client.TranscribeAsync(stream);

        Assert.NotNull(result);
        Assert.NotNull(result.Text);
        Assert.NotEmpty(result.Text);
    }

    [Fact(Timeout = TimeoutMs)]
    public async Task TranscriptionResult_HasLanguageAndDuration()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioPath = GetTestDataPath("test-audio-medium.wav");
        var result = await client.TranscribeAsync(audioPath);

        Assert.Equal("en", result.DetectedLanguage);
        Assert.True(result.Duration > TimeSpan.Zero, "Duration should be positive");
    }
}
