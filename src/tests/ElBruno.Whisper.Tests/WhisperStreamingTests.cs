using System.Text;
using ElBruno.Whisper.Audio;
using Xunit;

namespace ElBruno.Whisper.Tests;

public class WhisperStreamingTests
{
    [Fact]
    public async Task GetStreamingTextAsync_EmitsOrderedUpdatesAndSingleFinalUpdate()
    {
        using var stream = CreateWavStream(duration: TimeSpan.FromSeconds(10));
        using var client = CreateStreamingClient(
            "hello world from",
            "world from whisper",
            "whisper library",
            "library final");

        var updates = await CollectUpdatesAsync(
            client.GetStreamingTextAsync(stream, new WhisperStreamingOptions
            {
                WindowSize = TimeSpan.FromSeconds(4),
                StepSize = TimeSpan.FromSeconds(2),
                ContextOverlap = TimeSpan.FromSeconds(1)
            }));

        Assert.NotEmpty(updates);
        Assert.Single(updates, static update => update.IsFinal);
        Assert.True(updates[^1].IsFinal, "The last update must be final.");
        Assert.Equal("hello world from whisper library final", updates[^1].Text);
        Assert.Equal(updates[^1].CommittedText, updates[^1].Text);
        Assert.Equal(string.Empty, updates[^1].ProvisionalText);

        for (int index = 1; index < updates.Count; index++)
        {
            Assert.True(updates[index].WindowEnd >= updates[index - 1].WindowEnd);
        }
    }

    [Fact]
    public async Task GetStreamingTextAsync_DoesNotDuplicateCommittedText()
    {
        using var stream = CreateWavStream(duration: TimeSpan.FromSeconds(8));
        using var client = CreateStreamingClient(
            "we know technology",
            "know technology is advancing",
            "is advancing quickly",
            "quickly today");

        var updates = await CollectUpdatesAsync(
            client.GetStreamingTextAsync(stream, new WhisperStreamingOptions
            {
                WindowSize = TimeSpan.FromSeconds(3),
                StepSize = TimeSpan.FromSeconds(2),
                ContextOverlap = TimeSpan.FromSeconds(1)
            }));

        var finalUpdate = updates.Single(static update => update.IsFinal);
        Assert.Equal("we know technology is advancing quickly today", finalUpdate.Text);
        Assert.DoesNotContain("technology technology", finalUpdate.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("advancing advancing", finalUpdate.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStreamingTextAsync_CancellationStopsWithoutFinalUpdate()
    {
        using var stream = CreateWavStream(duration: TimeSpan.FromSeconds(8));
        using var client = CreateStreamingClient(
            "alpha beta",
            "beta gamma",
            "gamma delta");

        using var cancellationTokenSource = new CancellationTokenSource();
        var updates = new List<StreamingTranscriptionUpdate>();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var update in client.GetStreamingTextAsync(
                               stream,
                               new WhisperStreamingOptions
                               {
                                   WindowSize = TimeSpan.FromSeconds(3),
                                   StepSize = TimeSpan.FromSeconds(2),
                                   ContextOverlap = TimeSpan.FromSeconds(1)
                               },
                               cancellationTokenSource.Token))
            {
                updates.Add(update);
                cancellationTokenSource.Cancel();
            }
        });

        Assert.NotEmpty(updates);
        Assert.DoesNotContain(updates, static update => update.IsFinal);
    }

    [Fact]
    public async Task GetStreamingTextAsync_BlankHypothesesDoNotFabricateText()
    {
        using var stream = CreateWavStream(duration: TimeSpan.FromSeconds(5));
        using var client = CreateStreamingClient("", "", "");

        var updates = await CollectUpdatesAsync(
            client.GetStreamingTextAsync(stream, new WhisperStreamingOptions
            {
                WindowSize = TimeSpan.FromSeconds(2),
                StepSize = TimeSpan.FromSeconds(1),
                ContextOverlap = TimeSpan.FromMilliseconds(500)
            }));

        var finalUpdate = updates.Single(static update => update.IsFinal);
        Assert.Equal(string.Empty, finalUpdate.Text);
        Assert.Equal(string.Empty, finalUpdate.CommittedText);
        Assert.Equal(string.Empty, finalUpdate.ProvisionalText);
    }

    [Fact]
    public async Task GetStreamingTextAsync_InvalidStreamingOptionsThrow()
    {
        using var stream = CreateWavStream(duration: TimeSpan.FromSeconds(3));
        using var client = CreateStreamingClient("hello");

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await foreach (var _ in client.GetStreamingTextAsync(stream, new WhisperStreamingOptions
                           {
                               WindowSize = TimeSpan.FromSeconds(2),
                               StepSize = TimeSpan.FromSeconds(1),
                               ContextOverlap = TimeSpan.FromSeconds(2)
                           }))
            {
            }
        });

        Assert.Equal(nameof(WhisperStreamingOptions.ContextOverlap), ex.ParamName);
    }

    private static WhisperClient CreateStreamingClient(params string[] hypotheses)
    {
        return new WhisperClient(
            new WhisperOptions(),
            new AudioProcessor(),
            new FakeTranscriptionBackend(hypotheses));
    }

    private static async Task<List<StreamingTranscriptionUpdate>> CollectUpdatesAsync(
        IAsyncEnumerable<StreamingTranscriptionUpdate> updates)
    {
        var list = new List<StreamingTranscriptionUpdate>();
        await foreach (var update in updates)
        {
            list.Add(update);
        }

        return list;
    }

    private static MemoryStream CreateWavStream(TimeSpan duration)
    {
        const int sampleRate = AudioProcessor.TargetSampleRate;
        const short channels = 1;
        const short bitsPerSample = 16;
        var sampleCount = Math.Max(1, (int)Math.Round(duration.TotalSeconds * sampleRate));
        var dataSize = sampleCount * sizeof(short);

        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * (bitsPerSample / 8));
            writer.Write((short)(channels * (bitsPerSample / 8)));
            writer.Write(bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            for (int index = 0; index < sampleCount; index++)
            {
                writer.Write((short)0);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private sealed class FakeTranscriptionBackend : IWhisperTranscriptionBackend
    {
        private readonly string[] _hypotheses;
        private int _index;

        public FakeTranscriptionBackend(params string[] hypotheses)
        {
            _hypotheses = hypotheses;
        }

        public Task<TranscriptionResult> TranscribeAsync(
            float[] melSpectrogram,
            TimeSpan audioDuration,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hypothesis = _index < _hypotheses.Length
                ? _hypotheses[_index]
                : _hypotheses[^1];

            _index++;

            return Task.FromResult(new TranscriptionResult
            {
                Text = hypothesis,
                Duration = audioDuration
            });
        }

        public void Dispose()
        {
        }
    }
}
