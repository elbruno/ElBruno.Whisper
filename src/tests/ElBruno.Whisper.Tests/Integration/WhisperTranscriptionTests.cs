using Xunit;
#pragma warning disable MEAI001
using Microsoft.Extensions.AI;

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
    /// Regression test for GitHub issue #10: Inference returns empty text.
    /// Audio sample rate is 48kHz stereo, requires resampling to 16kHz mono.
    /// Expected text: "We know technology is advancing quickly, but AI is moving even faster."
    /// </summary>
    [Fact(Timeout = TimeoutMs)]
    public async Task Transcribe48kHzStereoAudio_ReturnsExpectedText_Issue10()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioPath = GetTestDataPath("test-audio-48khz-stereo.wav");
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
    /// Regression test for GitHub issue #10: Inference returns empty text.
    /// Audio is already 16kHz mono (native Whisper format).
    /// Expected text: "We know technology is advancing quickly, but AI is moving even faster."
    /// </summary>
    [Fact(Timeout = TimeoutMs)]
    public async Task Transcribe16kHzMonoAudio_ReturnsExpectedText_Issue10()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioPath = GetTestDataPath("test-audio-16khz-mono.wav");
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
    public async Task SpeechToTextAdapter_SmallAudio_ReturnsNonEmptyText()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var whisperClient = await WhisperClient.CreateAsync(options);
        using ISpeechToTextClient client = new WhisperSpeechToTextClient(whisperClient);

        var audioPath = GetTestDataPath("test-audio-small.wav");
        using var stream = File.OpenRead(audioPath);
        var result = await client.GetTextAsync(
            stream,
            new SpeechToTextOptions
            {
                SpeechLanguage = "en"
            });

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

    [Fact(Timeout = TimeoutMs)]
    public async Task TranscribeAudio_WithTimestamps_ReturnsSegmentsAndWords()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en",
            EnableTimestamps = true
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioPath = GetTestDataPath("test-audio-small.wav");
        var result = await client.TranscribeAsync(audioPath);

        Assert.NotNull(result.Segments);
        Assert.NotNull(result.Words);
        Assert.NotEmpty(result.Segments);
        Assert.NotEmpty(result.Words);

        var flattenedWords = result.Segments.SelectMany(static segment => segment.Words).ToList();

        Assert.Equal(flattenedWords.Count, result.Words.Count);

        foreach (var segment in result.Segments)
        {
            Assert.True(segment.End >= segment.Start, "Segment end should be at or after its start.");
            Assert.NotEmpty(segment.Text);
            Assert.NotEmpty(segment.Words);

            foreach (var word in segment.Words)
            {
                Assert.True(word.Start >= segment.Start, "Word start should stay within the segment.");
                Assert.True(word.End <= segment.End, "Word end should stay within the segment.");
                Assert.True(word.End >= word.Start, "Word end should be at or after the word start.");
                Assert.False(string.IsNullOrWhiteSpace(word.Text));
            }
        }
    }

    [Fact(Timeout = TimeoutMs)]
    public async Task ConcurrentTranscribe_WithConcurrencyLimitOne_QueuesRequestsSafely()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en",
            Concurrency = new WhisperConcurrencyOptions
            {
                MaximumConcurrentRequests = 1,
                QueueTimeout = TimeSpan.FromMinutes(2)
            }
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioOne = GetTestDataPath("test-audio-small.wav");
        var audioTwo = GetTestDataPath("test-audio-medium.wav");

        var results = await Task.WhenAll(
            client.TranscribeAsync(audioOne),
            client.TranscribeAsync(audioTwo));

        Assert.All(results, static result => Assert.False(string.IsNullOrWhiteSpace(result.Text)));
    }

    [Fact(Timeout = TimeoutMs)]
    public async Task ConcurrentTranscribe_WithConcurrencyLimitTwo_ProcessesSharedClientRequests()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en",
            Concurrency = new WhisperConcurrencyOptions
            {
                MaximumConcurrentRequests = 2,
                QueueTimeout = TimeSpan.FromMinutes(2),
                EnableSessionPooling = true
            }
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioOne = GetTestDataPath("test-audio-small.wav");
        var audioTwo = GetTestDataPath("test-audio-medium.wav");

        var results = await Task.WhenAll(
            client.TranscribeAsync(audioOne),
            client.TranscribeAsync(audioTwo));

        Assert.All(results, static result => Assert.False(string.IsNullOrWhiteSpace(result.Text)));
    }

    [Fact(Timeout = TimeoutMs)]
    public async Task GetStreamingTextAsync_EmitsProvisionalAndFinalUpdates()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn,
            EnsureModelDownloaded = true,
            Language = "en"
        };

        using var client = await WhisperClient.CreateAsync(options);

        var audioPath = GetTestDataPath("test-audio-medium.wav");
        var updates = new List<StreamingTranscriptionUpdate>();

        await foreach (var update in client.GetStreamingTextAsync(audioPath, new WhisperStreamingOptions
                       {
                           WindowSize = TimeSpan.FromSeconds(2),
                           StepSize = TimeSpan.FromSeconds(1),
                           ContextOverlap = TimeSpan.FromMilliseconds(500)
                       }))
        {
            updates.Add(update);
        }

        Assert.NotEmpty(updates);
        Assert.True(updates.Count > 1, "Expected at least one rolling update before the final update.");
        Assert.Single(updates, static update => update.IsFinal);
        Assert.True(updates[^1].IsFinal, "The final update should be the last update emitted.");
        Assert.False(string.IsNullOrWhiteSpace(updates[^1].Text));
        Assert.Contains(updates, static update => !update.IsFinal && !string.IsNullOrWhiteSpace(update.ProvisionalText));
    }
}
