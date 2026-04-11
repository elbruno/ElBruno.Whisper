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

    /// <summary>
    /// Regression test for GitHub issue #10: Inference returns empty text for trump.wav.
    /// Audio sample rate is 48kHz stereo, requires resampling to 16kHz mono.
    /// Expected text: "We know technology is advancing quickly, but AI is moving even faster."
    /// </summary>
    [Fact(Timeout = TimeoutMs)]
    public async Task TranscribeTrumpAudio_ReturnsExpectedText_Issue10()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioPath = GetTestDataPath("trump.wav");
        var result = await client.TranscribeAsync(audioPath);

        Assert.NotNull(result);
        Assert.NotNull(result.Text);
        Assert.NotEmpty(result.Text);

        // Verify the transcription contains key phrases from the expected text
        var text = result.Text.ToLowerInvariant();
        Assert.Contains("technology", text);
        Assert.Contains("ai", text);
    }

    /// <summary>
    /// Regression test for GitHub issue #10: Inference returns empty text for trump16.wav.
    /// Audio is already 16kHz mono.
    /// Expected text: "We know technology is advancing quickly, but AI is moving even faster."
    /// </summary>
    [Fact(Timeout = TimeoutMs)]
    public async Task TranscribeTrump16Audio_ReturnsExpectedText_Issue10()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioPath = GetTestDataPath("trump16.wav");
        var result = await client.TranscribeAsync(audioPath);

        Assert.NotNull(result);
        Assert.NotNull(result.Text);
        Assert.NotEmpty(result.Text);

        // Verify the transcription contains key phrases from the expected text
        var text = result.Text.ToLowerInvariant();
        Assert.Contains("technology", text);
        Assert.Contains("ai", text);
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
