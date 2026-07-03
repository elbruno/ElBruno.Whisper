# API Reference

## WhisperClient

Main class for speech-to-text transcription.

`WhisperClient` is safe to share across concurrent callers. Concurrency behavior is configured through `WhisperOptions.Concurrency`.

### Creating a Client

```csharp
// Default (tiny.en model)
using var client = await WhisperClient.CreateAsync();

// With options
using var client = await WhisperClient.CreateAsync(new WhisperOptions { ... });

// With progress tracking
using var client = await WhisperClient.CreateAsync(
    progress: new Progress<DownloadProgress>(p => { ... })
);
```

### Methods

#### TranscribeAsync

Transcribe an audio file to text.

```csharp
public async Task<TranscriptionResult> TranscribeAsync(
    string audioFilePath,
    CancellationToken cancellationToken = default);
```

**Parameters:**
- `audioFilePath` (string) — Path to audio file (WAV, MP3, etc.)
- `cancellationToken` (CancellationToken, optional) — For cancellation support

**Returns:** `TranscriptionResult` containing transcribed text and metadata

**Example:**
```csharp
using var client = await WhisperClient.CreateAsync(new WhisperOptions
{
    Language = "es"
});

var result = await client.TranscribeAsync("audio.wav");
Console.WriteLine(result.Text);
```

**Exceptions:**
- `FileNotFoundException` — Audio file not found
- `InvalidOperationException` — Model failed to load or transcription error
- `OperationCanceledException` — Transcription was cancelled
- `TimeoutException` — The request waited longer than `WhisperOptions.Concurrency.QueueTimeout`

#### GetStreamingTextAsync

Emit rolling transcription updates for a file or stream.

```csharp
public IAsyncEnumerable<StreamingTranscriptionUpdate> GetStreamingTextAsync(
    string audioFilePath,
    WhisperStreamingOptions? streamingOptions = null,
    CancellationToken cancellationToken = default);

public IAsyncEnumerable<StreamingTranscriptionUpdate> GetStreamingTextAsync(
    Stream audioStream,
    WhisperStreamingOptions? streamingOptions = null,
    CancellationToken cancellationToken = default);
```

**Parameters:**
- `audioFilePath` / `audioStream` — Completed audio content to analyze with rolling windows
- `streamingOptions` (`WhisperStreamingOptions`, optional) — Window sizing and local-agreement behavior
- `cancellationToken` (`CancellationToken`, optional) — Stops future updates and prevents the final flush

**Returns:** `IAsyncEnumerable<StreamingTranscriptionUpdate>` with ordered provisional and final updates

**Example:**
```csharp
await foreach (var update in client.GetStreamingTextAsync(
                   "audio.wav",
                   new WhisperStreamingOptions
                   {
                       WindowSize = TimeSpan.FromSeconds(8),
                       StepSize = TimeSpan.FromSeconds(1),
                       ContextOverlap = TimeSpan.FromSeconds(2),
                       UseLocalAgreement = true,
                       AgreementIterations = 2
                   }))
{
    Console.WriteLine($"Committed: {update.CommittedText}");
    Console.WriteLine($"Provisional: {update.ProvisionalText}");
}
```

**Notes:**
- `IsFinal` is `true` exactly once on the last successful update.
- `CommittedText` is de-duplicated across overlapping windows.
- `ProvisionalText` may change between updates because Whisper is not a native streaming model.

#### Dispose

Release model and ONNX Runtime resources.

```csharp
client.Dispose();
// or use a `using` statement
using var client = await WhisperClient.CreateAsync();
```

---

## WhisperOptions

Configuration for WhisperClient.

```csharp
public class WhisperOptions
{
    // Which model to use (default: WhisperTinyEn)
    public string Model { get; set; } = KnownWhisperModels.WhisperTinyEn;
    
    // Skip download if already cached (default: true)
    public bool EnsureModelDownloaded { get; set; } = true;
    
    // Language hint (ISO 639-1 code)
    public string? Language { get; set; }

    // Translate audio to English instead of transcribing
    public bool Translate { get; set; }

    // Maximum number of output tokens
    public int MaxTokens { get; set; } = 448;
    
    // Sampling temperature 0-1 (default: 0.0 for deterministic)
    public float Temperature { get; set; } = 0.0f;

    // Include segment and word timestamps
    public bool EnableTimestamps { get; set; }

    // Concurrent transcription settings
    public WhisperConcurrencyOptions Concurrency { get; set; } = new();
}
```

### Example

```csharp
var options = new WhisperOptions
{
    Model = KnownWhisperModels.WhisperMedium,
    Language = "en",
    EnableTimestamps = true,
    Temperature = 0.2f
};
using var client = await WhisperClient.CreateAsync(options);
```

---

## WhisperConcurrencyOptions

Controls how a shared `WhisperClient` handles concurrent requests.

```csharp
public sealed class WhisperConcurrencyOptions
{
    public int MaximumConcurrentRequests { get; set; } = 1;
    public TimeSpan QueueTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableSessionPooling { get; set; } = true;
}
```

### Example

```csharp
var options = new WhisperOptions
{
    Model = KnownWhisperModels.WhisperTinyEn,
    Concurrency = new WhisperConcurrencyOptions
    {
        MaximumConcurrentRequests = 2,
        QueueTimeout = TimeSpan.FromSeconds(15),
        EnableSessionPooling = true
    }
};
```

`MaximumConcurrentRequests` controls how many transcriptions may run in parallel. `QueueTimeout` bounds how long a caller may wait for a free inference slot. `EnableSessionPooling` reuses ONNX sessions between requests to avoid paying the full session-creation cost every time.

---

## WhisperStreamingOptions

Controls rolling-window update behavior for `GetStreamingTextAsync()`.

```csharp
public sealed class WhisperStreamingOptions
{
    public TimeSpan WindowSize { get; set; } = TimeSpan.FromSeconds(8);
    public TimeSpan StepSize { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan ContextOverlap { get; set; } = TimeSpan.FromSeconds(2);
    public bool UseLocalAgreement { get; set; } = true;
    public int AgreementIterations { get; set; } = 2;
}
```

### Behavior

- `WindowSize` controls how much new audio each rolling pass analyzes.
- `StepSize` controls how often a new update can be emitted.
- `ContextOverlap` keeps a short lookback window so adjacent passes share context.
- `UseLocalAgreement` delays commits until neighboring hypotheses overlap.
- `AgreementIterations` controls how many successive hypotheses are required before the oldest one is committed.

**Validation rules:**
- `WindowSize` > 0
- `StepSize` > 0
- `ContextOverlap` >= 0 and `< WindowSize`
- `AgreementIterations` >= 1

---

## StreamingTranscriptionUpdate

Represents one incremental update from `GetStreamingTextAsync()`.

```csharp
public sealed record StreamingTranscriptionUpdate
{
    public string Text { get; init; }
    public string CommittedText { get; init; }
    public string ProvisionalText { get; init; }
    public TimeSpan WindowStart { get; init; }
    public TimeSpan WindowEnd { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public bool IsFinal { get; init; }
}
```

### Semantics

- `Text` is `CommittedText` plus the current `ProvisionalText`
- `CommittedText` contains stable text only
- `ProvisionalText` contains the latest revisable tail
- `WindowStart` / `WindowEnd` describe the rolling window that produced the update
- `IsFinal` is `true` only for the terminal flush

---

## KnownWhisperModels

Constants for all available Whisper model IDs.

### English-Optimized Models

```csharp
KnownWhisperModels.WhisperTinyEn      // 39M parameters, 75 MB
KnownWhisperModels.WhisperBaseEn      // 74M parameters, 140 MB
KnownWhisperModels.WhisperSmallEn     // 244M parameters, 460 MB
KnownWhisperModels.WhisperMediumEn    // 769M parameters, 1.5 GB
```

### Multilingual Models

```csharp
KnownWhisperModels.WhisperTiny        // 39M parameters, 75 MB
KnownWhisperModels.WhisperBase        // 74M parameters, 140 MB
KnownWhisperModels.WhisperSmall       // 244M parameters, 460 MB
KnownWhisperModels.WhisperMedium      // 769M parameters, 1.5 GB
KnownWhisperModels.WhisperLarge       // 1550M parameters, 3.0 GB
```

### Example

```csharp
// Use small English-optimized model
var options = new WhisperOptions
{
    Model = KnownWhisperModels.WhisperSmallEn
};
```

---

## TranscriptionResult

Result from WhisperClient.TranscribeAsync().

```csharp
public class TranscriptionResult
{
    // Transcribed text
    public string Text { get; set; }
    
    // Detected language (ISO 639-1 code, null for English models)
    public string? DetectedLanguage { get; set; }
    
    // Duration of audio
    public TimeSpan Duration { get; set; }

    // Timestamped segments when EnableTimestamps is true
    public IReadOnlyList<TranscriptionSegment>? Segments { get; set; }

    // Flattened timestamped words when EnableTimestamps is true
    public IReadOnlyList<TranscriptionWord>? Words { get; set; }
}
```

### Example

```csharp
var result = await client.TranscribeAsync("audio.wav");

Console.WriteLine($"Text: {result.Text}");
Console.WriteLine($"Language: {result.DetectedLanguage}");
Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");

foreach (var word in result.Words ?? [])
{
    Console.WriteLine($"{word.Start:mm\\:ss\\.ff} - {word.End:mm\\:ss\\.ff}: {word.Text}");
}
```

### TranscriptionSegment

```csharp
public class TranscriptionSegment
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public string Text { get; set; }
    public IReadOnlyList<TranscriptionWord> Words { get; set; }
}
```

### TranscriptionWord

```csharp
public class TranscriptionWord
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public string Text { get; set; }
}
```

Word timings are derived from each timestamped transcript span. If a model returns text without explicit spans, the response falls back to a single full-duration segment and derives word timings within that range.

---

## WhisperModelDefinition

Metadata about a Whisper model.

```csharp
public class WhisperModelDefinition
{
    // Model ID (e.g., "openai/whisper-tiny.en")
    public string Id { get; set; }
    
    // Human-readable name
    public string Name { get; set; }
    
    // Parameter count
    public long Parameters { get; set; }
    
    // Download size in bytes
    public long SizeBytes { get; set; }
    
    // Whether this is English-only
    public bool IsEnglishOnly { get; set; }
}
```

---

## Dependency Injection Extensions

### AddWhisper

Register Whisper options in ASP.NET Core or other DI containers.

```csharp
// Program.cs
builder.Services.AddWhisper(options =>
{
    options.Model = KnownWhisperModels.WhisperSmallEn;
    options.Language = "en";
    options.Concurrency.MaximumConcurrentRequests = 2;
});
```

`AddWhisper()` registers `WhisperOptions`. Create and manage the lifetime of `WhisperClient` yourself so you can decide when to share it and when to dispose it.

---

## ElBruno.HuggingFace Integration

Whisper uses the ElBruno.HuggingFace library for model downloads.

### DownloadProgress

Receives progress updates during model download.

```csharp
public class DownloadProgress
{
    // Current stage of download
    public DownloadStage Stage { get; set; }
    
    // Current file being processed
    public string CurrentFile { get; set; }
    
    // Download percentage (0-100)
    public double PercentComplete { get; set; }
    
    // Status message
    public string Message { get; set; }
}

public enum DownloadStage
{
    Checking,     // Verifying model exists
    Downloading,  // Downloading model files
    Verifying,    // Verifying checksums
    Completed     // Download complete
}
```

### Example

```csharp
var progress = new Progress<DownloadProgress>(p =>
{
    if (p.Stage == DownloadStage.Downloading)
        Console.Write($"\r⬇️ {p.CurrentFile}: {p.PercentComplete:F0}%");
    else
        Console.WriteLine($"✓ {p.Message}");
});

using var client = await WhisperClient.CreateAsync(progress: progress);
```

---

## Error Handling

Common exceptions and how to handle them:

```csharp
try
{
    using var client = await WhisperClient.CreateAsync();
    var result = await client.TranscribeAsync("audio.wav");
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"Audio file not found: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Operation error: {ex.Message}");
}
catch (OperationCanceledException ex)
{
    Console.WriteLine($"Transcription cancelled: {ex.Message}");
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Transcription queue timeout: {ex.Message}");
}
```
