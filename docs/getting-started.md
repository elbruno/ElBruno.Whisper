# Getting Started with ElBruno.Whisper

## Installation

### Prerequisites

- .NET 8.0 or later
- An audio file in WAV, MP3, or other supported format

### Install via NuGet

```bash
dotnet add package ElBruno.Whisper
```

## Quick Start (5 minutes)

### 1. Create a Console App

```bash
dotnet new console -n WhisperDemo
cd WhisperDemo
dotnet add package ElBruno.Whisper
```

### 2. Add Code

```csharp
using ElBruno.Whisper;

Console.WriteLine("🎤 Whisper Transcription Demo");

// Create client (downloads model on first run)
using var client = await WhisperClient.CreateAsync();

// Transcribe audio
var result = await client.TranscribeAsync("audio.wav");

Console.WriteLine($"📝 Result: {result.Text}");
if (result.DetectedLanguage is not null)
    Console.WriteLine($"🌍 Language: {result.DetectedLanguage}");
Console.WriteLine($"⏱️ Duration: {result.Duration.TotalSeconds:F1}s");
```

### 3. Run

```bash
dotnet run -- audio.wav
```

On first run, the tiny.en model (~75 MB) is downloaded automatically.

## Model Selection Guide

Choose your model based on:
- **Accuracy needed** — larger models are more accurate
- **Speed required** — smaller models are faster
- **Languages** — use multilingual models for non-English audio
- **Available memory** — match your hardware

### Scenarios

**Fast transcription of English audio (web app):**
```csharp
var options = new WhisperOptions 
{ 
    Model = KnownWhisperModels.WhisperTinyEn  // 39M, 75MB, ⚡⚡⚡⚡⚡
};
using var client = await WhisperClient.CreateAsync(options);
```

**Accurate transcription of any language:**
```csharp
var options = new WhisperOptions 
{ 
    Model = KnownWhisperModels.WhisperMedium  // 769M, 1.5GB, accurate
};
using var client = await WhisperClient.CreateAsync(options);
```

**Best accuracy for production systems:**
```csharp
var options = new WhisperOptions 
{ 
    Model = KnownWhisperModels.WhisperLarge  // 1550M, 3GB, highest accuracy
};
using var client = await WhisperClient.CreateAsync(options);
```

## Configuration Options

### WhisperOptions Properties

```csharp
var options = new WhisperOptions
{
    // Which Whisper model to use
    Model = KnownWhisperModels.WhisperBaseEn,
    
    // Skip download if model already cached
    EnsureModelDownloaded = true,
    
    // Language hint (ISO 639-1 code, e.g., "en", "es", "fr")
    Language = null,

    // Include segment and word timestamps in the response
    EnableTimestamps = false,
    
    // Temperature for sampling (0-1, higher = more varied)
    Temperature = 0.0f,

    // Share a client safely across concurrent callers
    Concurrency = new WhisperConcurrencyOptions
    {
        MaximumConcurrentRequests = 2,
        QueueTimeout = TimeSpan.FromSeconds(15),
        EnableSessionPooling = true
    }
};

using var client = await WhisperClient.CreateAsync(options);
```

## Progress Tracking

Monitor model downloads:

```csharp
var downloadProgress = new Progress<ElBruno.HuggingFace.DownloadProgress>(p =>
{
    switch (p.Stage)
    {
        case ElBruno.HuggingFace.DownloadStage.Checking:
            Console.WriteLine($"✓ Checking {p.CurrentFile}...");
            break;
        case ElBruno.HuggingFace.DownloadStage.Downloading:
            Console.Write($"\r⬇️ {p.PercentComplete:F0}%");
            break;
        case ElBruno.HuggingFace.DownloadStage.Verifying:
            Console.WriteLine($"\n✓ Verifying {p.CurrentFile}...");
            break;
        case ElBruno.HuggingFace.DownloadStage.Completed:
            Console.WriteLine($"✓ Ready!");
            break;
    }
});

using var client = await WhisperClient.CreateAsync(progress: downloadProgress);
```

## Concurrent Transcription

`WhisperClient` is safe to share across concurrent callers. The default concurrency limit is 1, which preserves serialized inference. Increase it when you want a shared client to process multiple requests in parallel:

```csharp
using var client = await WhisperClient.CreateAsync(new WhisperOptions
{
    Model = KnownWhisperModels.WhisperTinyEn,
    Concurrency = new WhisperConcurrencyOptions
    {
        MaximumConcurrentRequests = 2,
        QueueTimeout = TimeSpan.FromSeconds(15),
        EnableSessionPooling = true
    }
});

var results = await Task.WhenAll(
    client.TranscribeAsync("call-1.wav"),
    client.TranscribeAsync("call-2.wav"));
```

If all inference slots stay busy longer than `QueueTimeout`, the waiting request throws `TimeoutException`. Cancelling the request also stops queue waiting and is observed during decoding at the next safe checkpoint.

## Microsoft.Extensions.AI Integration

If your app already composes AI providers through `Microsoft.Extensions.AI`, register the adapter and resolve `ISpeechToTextClient`:

```csharp
using ElBruno.Whisper;
using Microsoft.Extensions.AI;

builder.Services.AddWhisper(options =>
{
    options.Model = KnownWhisperModels.WhisperTinyEn;
    options.Language = "en";
});

var services = builder.Services.BuildServiceProvider();
var speechToTextClient = services.GetRequiredService<ISpeechToTextClient>();

await using var stream = File.OpenRead("audio.wav");
var response = await speechToTextClient.GetTextAsync(
    stream,
    new SpeechToTextOptions
    {
        SpeechLanguage = "en",
        AdditionalProperties = new()
        {
            ["elbruno.whisper.enable_timestamps"] = true
        }
    });

Console.WriteLine(response.Text);
```

The adapter returns Whisper metadata through `SpeechToTextResponse.AdditionalProperties` and keeps the input stream open for the caller.

## Incremental Transcription

Use `GetStreamingTextAsync()` when you want rolling updates while processing an already available audio file or stream:

```csharp
using var client = await WhisperClient.CreateAsync();

await foreach (var update in client.GetStreamingTextAsync(
                   "audio.wav",
                   new WhisperStreamingOptions
                   {
                       WindowSize = TimeSpan.FromSeconds(8),
                       StepSize = TimeSpan.FromSeconds(1),
                       ContextOverlap = TimeSpan.FromSeconds(2)
                   }))
{
    Console.WriteLine($"Committed: {update.CommittedText}");
    Console.WriteLine($"Provisional: {update.ProvisionalText}");
}
```

`CommittedText` only includes text that has stabilized across overlapping windows. `ProvisionalText` is the revisable tail. `IsFinal` becomes `true` exactly once on the last update.

> **Important:** Whisper is not a true streaming model. The library simulates incremental output by re-running inference over overlapping windows, so provisional text can change between updates.

## Working with WAV Streams and Raw PCM

Use the stream overload directly when you already have WAV content in memory:

```csharp
using var client = await WhisperClient.CreateAsync();

await using var wavStream = File.OpenRead("audio.wav");
var wavResult = await client.TranscribeAsync(wavStream);
```

For raw PCM bytes or non-file streams, provide an explicit `WhisperAudioFormat`:

```csharp
var format = new WhisperAudioFormat(
    sampleRate: 48000,
    channels: 2,
    sampleFormat: WhisperAudioSampleFormat.Pcm16);

await using var rawPcmStream = File.OpenRead("audio.pcm");
var rawResult = await client.TranscribeAsync(rawPcmStream, format);
```

If you already have normalized float samples in memory, use the float overload:

```csharp
ReadOnlyMemory<float> monoSamples = GetMicrophoneBuffer();

using var client = await WhisperClient.CreateAsync();
var result = await client.TranscribeAsync(monoSamples, sampleRate: 16000);
```

These overloads:

- Keep ownership of the input stream with the caller
- Avoid temporary WAV files
- Downmix multi-channel input to mono and resample to 16 kHz automatically
- Throw `WhisperAudioFormatException` when the input bytes do not match the provided format

## Transcription

### Basic Transcription

```csharp
using var client = await WhisperClient.CreateAsync();

var result = await client.TranscribeAsync("path/to/audio.wav");
Console.WriteLine(result.Text);
```

### With Language Hints

If you know the audio language, set it in the client options to improve accuracy:

```csharp
using var client = await WhisperClient.CreateAsync(new WhisperOptions
{
    Language = "es"
});

var result = await client.TranscribeAsync("spanish-audio.wav");
```

### Accessing Result Details

```csharp
var result = await client.TranscribeAsync("audio.wav");

// Transcribed text
string text = result.Text;

// Detected language (null for English-optimized models)
string? language = result.DetectedLanguage;

// Audio duration
TimeSpan duration = result.Duration;
```

### With Segment and Word Timestamps

```csharp
using var client = await WhisperClient.CreateAsync(new WhisperOptions
{
    EnableTimestamps = true
});

var result = await client.TranscribeAsync("audio.wav");

foreach (var segment in result.Segments ?? [])
{
    Console.WriteLine($"{segment.Start:mm\\:ss\\.ff} - {segment.End:mm\\:ss\\.ff}: {segment.Text}");

    foreach (var word in segment.Words)
    {
        Console.WriteLine($"  {word.Start:mm\\:ss\\.ff} - {word.End:mm\\:ss\\.ff}: {word.Text}");
    }
}
```

Word timings are derived from the timestamped transcript spans returned by Whisper. If a model returns text without explicit spans, the response falls back to a single full-duration segment and derives word timings within that range.

## Dependency Injection (ASP.NET Core)

Register Whisper options in your service container:

```csharp
// Program.cs
builder.Services.AddWhisper(options =>
{
    options.Model = KnownWhisperModels.WhisperSmallEn;
    options.Concurrency.MaximumConcurrentRequests = 2;
});
```

`AddWhisper()` registers `WhisperOptions`. Create and own the `WhisperClient` in the service that needs it so you can decide whether to share one client and how to dispose it.

## Troubleshooting

### Model Download Fails

**Problem:** Download times out or fails with network error.

**Solution:**
1. Check your internet connection
2. Ensure HuggingFace is accessible from your network
3. For private models, set `HF_TOKEN` environment variable:
   ```bash
   set HF_TOKEN=hf_your_token_here
   dotnet run
   ```

### Out of Memory During Transcription

**Problem:** Application crashes with OutOfMemoryException.

**Solution:**
- Use a smaller model (tiny or base)
- Reduce audio length (chunk long files)
- Increase available RAM or run on a machine with more memory

### Wrong Language Detected

**Problem:** Multilingual model detects language incorrectly.

**Solution:**
- Use English-optimized model (`.en`) if audio is English
- Provide language hint via `language` parameter
- Use larger model for better accuracy

## Next Steps

- See [API Reference](api-reference.md) for detailed API documentation
- Review [Architecture](architecture.md) for how Whisper works
- Check out the [sample app](../src/samples/HelloWhisper/) for a complete example
