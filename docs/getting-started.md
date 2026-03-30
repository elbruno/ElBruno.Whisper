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
    
    // Temperature for sampling (0-1, higher = more varied)
    Temperature = 0.0f
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

## Transcription

### Basic Transcription

```csharp
using var client = await WhisperClient.CreateAsync();

var result = await client.TranscribeAsync("path/to/audio.wav");
Console.WriteLine(result.Text);
```

### With Language Hints

If you know the audio language, pass it to improve accuracy:

```csharp
var result = await client.TranscribeAsync("spanish-audio.wav", language: "es");
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

## Dependency Injection (ASP.NET Core)

Register Whisper in your service container:

```csharp
// Program.cs
builder.Services.AddWhisper(options =>
{
    options.Model = KnownWhisperModels.WhisperSmallEn;
});

// Inject into services
public class AudioService(WhisperClient whisper)
{
    public async Task<string> TranscribeAsync(string filePath)
    {
        var result = await whisper.TranscribeAsync(filePath);
        return result.Text;
    }
}
```

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
