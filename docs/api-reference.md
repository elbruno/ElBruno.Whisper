# API Reference

## WhisperClient

Main class for speech-to-text transcription.

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
    string? language = null,
    CancellationToken cancellationToken = default);
```

**Parameters:**
- `audioFilePath` (string) — Path to audio file (WAV, MP3, etc.)
- `language` (string?, optional) — ISO 639-1 language code (e.g., "en", "es", "fr")
- `cancellationToken` (CancellationToken, optional) — For cancellation support

**Returns:** `TranscriptionResult` containing transcribed text and metadata

**Example:**
```csharp
var result = await client.TranscribeAsync("audio.wav", language: "es");
Console.WriteLine(result.Text);
```

**Exceptions:**
- `FileNotFoundException` — Audio file not found
- `InvalidOperationException` — Model failed to load or transcription error
- `OperationCanceledException` — Transcription was cancelled

#### Dispose

Release model and ONNX Runtime resources.

```csharp
await client.DisposeAsync();
// or with `using` statement
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
    
    // Sampling temperature 0-1 (default: 0.0 for deterministic)
    public float Temperature { get; set; } = 0.0f;
}
```

### Example

```csharp
var options = new WhisperOptions
{
    Model = KnownWhisperModels.WhisperMedium,
    Language = "en",
    Temperature = 0.2f
};
using var client = await WhisperClient.CreateAsync(options);
```

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
}
```

### Example

```csharp
var result = await client.TranscribeAsync("audio.wav");

Console.WriteLine($"Text: {result.Text}");
Console.WriteLine($"Language: {result.DetectedLanguage}");
Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");
```

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

Register WhisperClient in ASP.NET Core or other DI containers.

```csharp
// Program.cs
builder.Services.AddWhisper(options =>
{
    options.Model = KnownWhisperModels.WhisperSmallEn;
    options.Language = "en";
});

// Inject into services
public class MyService(WhisperClient whisper)
{
    public async Task<string> Transcribe(string filePath)
    {
        var result = await whisper.TranscribeAsync(filePath);
        return result.Text;
    }
}
```

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
```
