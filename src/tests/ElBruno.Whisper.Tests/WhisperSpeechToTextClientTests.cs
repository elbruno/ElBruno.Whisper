#pragma warning disable MEAI001
using Microsoft.Extensions.AI;
using Xunit;

namespace ElBruno.Whisper.Tests;

public sealed class WhisperSpeechToTextClientTests
{
    [Fact]
    public void WhisperSpeechToTextClient_ImplementsSpeechToTextContract()
    {
        Assert.True(typeof(ISpeechToTextClient).IsAssignableFrom(typeof(WhisperSpeechToTextClient)));
    }

    [Fact]
    public async Task GetTextAsync_MapsResponseMetadata_AndLeavesStreamOpen()
    {
        var segments = new[]
        {
            new TranscriptionSegment
            {
                Start = TimeSpan.Zero,
                End = TimeSpan.FromSeconds(1),
                Text = "hola",
                Words =
                [
                    new TranscriptionWord
                    {
                        Start = TimeSpan.Zero,
                        End = TimeSpan.FromSeconds(1),
                        Text = "hola"
                    }
                ]
            }
        };

        using var whisperClient = CreateWhisperClient(new TranscriptionResult
        {
            Text = "hola",
            DetectedLanguage = "es",
            Duration = TimeSpan.FromSeconds(1),
            Segments = segments,
            Words = segments.SelectMany(static segment => segment.Words).ToArray()
        });

        using var adapter = new WhisperSpeechToTextClient(whisperClient);
        using var stream = CreateWavStream();

        var response = await adapter.GetTextAsync(stream);

        Assert.Equal("hola", response.Text);
        Assert.Equal(TimeSpan.Zero, response.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(1), response.EndTime);
        Assert.Equal(KnownWhisperModels.WhisperTinyEn.Id, response.ModelId);
        Assert.Null(adapter.GetService(typeof(Stream)));
        Assert.True(stream.CanRead);
        Assert.NotNull(response.AdditionalProperties);
        Assert.Equal("es", Assert.IsType<string>(response.AdditionalProperties![WhisperSpeechToTextClient.DetectedLanguageMetadataKey]));
        Assert.Equal(1000L, Assert.IsType<long>(response.AdditionalProperties[WhisperSpeechToTextClient.AudioDurationMetadataKey]));
        Assert.Equal(KnownWhisperModels.WhisperTinyEn.Id, Assert.IsType<string>(response.AdditionalProperties[WhisperSpeechToTextClient.ModelIdMetadataKey]));
        Assert.Equal("onnxruntime", Assert.IsType<string>(response.AdditionalProperties[WhisperSpeechToTextClient.ExecutionProviderMetadataKey]));
        Assert.Same(segments, response.AdditionalProperties[WhisperSpeechToTextClient.SegmentsMetadataKey]);
    }

    [Fact]
    public async Task GetTextAsync_MapsSpeechToTextOptionsToWhisperOptions()
    {
        WhisperOptions? capturedOptions = null;
        var adapter = new WhisperSpeechToTextClient(
            new WhisperOptions(),
            (options, cancellationToken) =>
            {
                capturedOptions = options;
                return Task.FromResult(CreateWhisperClient(new TranscriptionResult
                {
                    Text = "translated",
                    DetectedLanguage = "es",
                    Duration = TimeSpan.FromSeconds(2)
                }));
            });
        using var stream = CreateWavStream();

        var response = await adapter.GetTextAsync(
            stream,
            new SpeechToTextOptions
            {
                ModelId = KnownWhisperModels.WhisperBase.Id,
                SpeechLanguage = "es",
                TextLanguage = "en",
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["elbruno.whisper.enable_timestamps"] = true,
                    ["elbruno.whisper.max_tokens"] = 128,
                    ["elbruno.whisper.temperature"] = 0.2f
                }
            });

        Assert.Equal("translated", response.Text);
        Assert.NotNull(capturedOptions);
        Assert.Equal(KnownWhisperModels.WhisperBase.Id, capturedOptions!.Model.Id);
        Assert.Equal("es", capturedOptions.Language);
        Assert.True(capturedOptions.Translate);
        Assert.True(capturedOptions.EnableTimestamps);
        Assert.Equal(128, capturedOptions.MaxTokens);
        Assert.Equal(0.2f, capturedOptions.Temperature);
    }

    [Fact]
    public async Task GetTextAsync_RawAudioOptions_EnablePcmTranscription()
    {
        using var adapter = new WhisperSpeechToTextClient(
            new WhisperOptions(),
            static (options, cancellationToken) => Task.FromResult(CreateWhisperClient(new TranscriptionResult
            {
                Text = "raw",
                Duration = TimeSpan.FromSeconds(1)
            })));

        using var stream = new MemoryStream(CreateRawPcm16Bytes(sampleCount: 1600));
        var response = await adapter.GetTextAsync(
            stream,
            new SpeechToTextOptions
            {
                SpeechSampleRate = 16000,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["elbruno.whisper.media_type"] = "audio/raw",
                    ["elbruno.whisper.channels"] = 1,
                    ["elbruno.whisper.sample_format"] = "pcm16"
                }
            });

        Assert.Equal("raw", response.Text);
    }

    [Fact]
    public async Task GetTextAsync_HonorsCancellation()
    {
        var backend = new RecordingBackend(new TranscriptionResult
        {
            Text = "never",
            Duration = TimeSpan.FromSeconds(1)
        });

        using var adapter = new WhisperSpeechToTextClient(CreateWhisperClient(backend));
        using var stream = CreateWavStream();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.GetTextAsync(stream, cancellationToken: cancellationSource.Token));

        Assert.Equal(0, backend.CallCount);
    }

    [Fact]
    public async Task GetStreamingTextAsync_ReturnsSingleFinalUpdate()
    {
        using var adapter = new WhisperSpeechToTextClient(CreateWhisperClient(new TranscriptionResult
        {
            Text = "streamed",
            Duration = TimeSpan.FromSeconds(1)
        }));

        using var stream = CreateWavStream();
        var updates = new List<SpeechToTextResponseUpdate>();

        await foreach (var update in adapter.GetStreamingTextAsync(stream))
        {
            updates.Add(update);
        }

        var onlyUpdate = Assert.Single(updates);
        Assert.Equal("streamed", onlyUpdate.Text);
        Assert.Equal(SpeechToTextResponseUpdateKind.TextUpdated, onlyUpdate.Kind);
    }

    [Fact]
    public void GetService_ReturnsMetadata_AndWrappedClient()
    {
        using var whisperClient = CreateWhisperClient(new TranscriptionResult
        {
            Text = "hello",
            Duration = TimeSpan.FromSeconds(1)
        });
        using var adapter = new WhisperSpeechToTextClient(whisperClient);

        var metadata = Assert.IsType<SpeechToTextClientMetadata>(adapter.GetService(typeof(SpeechToTextClientMetadata)));
        Assert.Equal("elbruno.whisper", metadata.ProviderName);
        Assert.Equal(KnownWhisperModels.WhisperTinyEn.Id, metadata.DefaultModelId);
        Assert.Same(whisperClient, adapter.GetService(typeof(WhisperClient)));
        Assert.Same(adapter, adapter.GetService(typeof(ISpeechToTextClient)));
    }

    private static WhisperClient CreateWhisperClient(TranscriptionResult result)
    {
        return CreateWhisperClient(new RecordingBackend(result));
    }

    private static WhisperClient CreateWhisperClient(RecordingBackend backend)
    {
        return new WhisperClient(new WhisperOptions(), new ElBruno.Whisper.Audio.AudioProcessor(), backend);
    }

    private static MemoryStream CreateWavStream()
    {
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, leaveOpen: true);
        var sampleCount = 1600;
        var dataSize = sampleCount * sizeof(short);

        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(16000);
        writer.Write(16000 * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        for (int i = 0; i < sampleCount; i++)
        {
            writer.Write((short)0);
        }

        writer.Flush();
        return new MemoryStream(memoryStream.ToArray());
    }

    private static byte[] CreateRawPcm16Bytes(int sampleCount)
    {
        var bytes = new byte[sampleCount * sizeof(short)];
        return bytes;
    }

    private sealed class RecordingBackend : IWhisperTranscriptionBackend
    {
        private readonly TranscriptionResult _result;

        public RecordingBackend(TranscriptionResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<TranscriptionResult> TranscribeAsync(
            float[] melSpectrogram,
            TimeSpan audioDuration,
            CancellationToken cancellationToken)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_result);
        }

        public void Dispose()
        {
        }
    }
}
