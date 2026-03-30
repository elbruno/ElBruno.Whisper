# Architecture

## High-Level Design

ElBruno.Whisper provides a simplified wrapper around OpenAI's Whisper model running through ONNX Runtime. The architecture handles model discovery, automatic download, caching, and transcription.

```
┌─────────────────────────────────────────────────────────────────┐
│                        WhisperClient                            │
│                  (High-level .NET API)                          │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
     ┌───────────────────────────────────┐
     │   ElBruno.HuggingFace Utilities    │
     │  (Model Discovery & Caching)      │
     └────────────┬──────────────────────┘
                  │
        ┌─────────┴─────────┐
        │                   │
        ▼                   ▼
  ┌──────────────┐  ┌──────────────────┐
  │ Local Cache  │  │ HuggingFace Hub  │
  │  (~models)   │  │  (Download)      │
  └──────────────┘  └──────────────────┘
        │                   │
        └─────────┬─────────┘
                  │
                  ▼
   ┌──────────────────────────────────┐
   │    ONNX Runtime with GenAI       │
   │   (Model Inference Engine)       │
   └──────────────────────────────────┘
```

## Audio Processing Pipeline

When you call `TranscribeAsync(audioFile)`, the following happens:

```
Input Audio File
  │
  ├─ Load (WAV/MP3 → PCM samples)
  │
  ├─ Resample (convert to 16 kHz if needed)
  │
  ├─ Mel Spectrogram
  │  (time-frequency representation of audio)
  │
  ├─ Encode (Whisper encoder processes features)
  │
  ├─ Decode (Whisper decoder generates tokens)
  │
  ├─ Detokenize (tokens → text)
  │
  └─> Output: TranscriptionResult
      ├─ Text (transcribed text)
      ├─ DetectedLanguage (for multilingual models)
      └─ Duration (audio length)
```

### Detailed Steps

1. **Audio Loading** — reads WAV/MP3 file and extracts PCM audio samples
2. **Resampling** — converts audio to 16 kHz mono (Whisper's expected format)
3. **Mel Spectrogram** — converts time-domain audio to frequency-domain representation
4. **Encoder** — ONNX Whisper encoder processes spectrogram features into embeddings
5. **Decoder** — ONNX Whisper decoder auto-regressively generates token IDs
6. **Tokenizer** — converts token IDs back to text using BPE tokenizer

## Model Download Flow

```
WhisperClient.CreateAsync()
  │
  ├─ Check cache (~localappdata/ElBruno/Whisper/models)
  │  │
  │  ├─ Found? → Use cached model → Done ✓
  │  │
  │  └─ Not found?
  │       │
  │       ├─ Query HuggingFace Hub for model metadata
  │       │
  │       ├─ Download model files (encoder, decoder, tokenizer)
  │       │  │
  │       │  └─ Report progress (OnProgress callback)
  │       │
  │       ├─ Verify checksums
  │       │
  │       └─ Cache for future use ✓
  │
  └─ Initialize ONNX Runtime with model files
```

## Caching Strategy

Models are cached at:
- **Windows:** `%LOCALAPPDATA%\ElBruno\Whisper\models`
- **macOS/Linux:** `~/.local/share/ElBruno/Whisper/models`

### Cache Structure

```
models/
├── openai/
│   ├── whisper-tiny.en/
│   │   ├── encoder.onnx
│   │   ├── decoder.onnx
│   │   ├── tokenizer.json
│   │   └── model_info.json
│   ├── whisper-base/
│   │   └── ...
│   └── ...
```

**Benefits:**
- First run downloads model (~30-60 seconds)
- Subsequent runs load instantly from cache
- Shared cache across multiple applications
- Manual cache clearing: delete the models directory

## ONNX Runtime Integration

Whisper uses ONNX (Open Neural Network Exchange) format models, executed by Microsoft's ONNX Runtime:

```
ONNX Runtime
  ├─ CPU Provider (always available)
  ├─ CUDA Provider (NVIDIA GPU, optional)
  ├─ DirectML Provider (Windows GPU, optional)
  └─ TensorRT Provider (NVIDIA, optional)
```

**Provider Selection (default Auto):**
1. Try GPU providers first (CUDA → DirectML)
2. Fall back to CPU if GPU unavailable

### Performance Characteristics

| Model | Parameters | Load Time | Inference Time |
|-------|-----------|-----------|-----------------|
| tiny.en | 39M | <1s | ~0.5-1.5s/sec of audio (CPU) |
| base.en | 74M | <1s | ~1-2s/sec of audio (CPU) |
| small.en | 244M | 1-2s | ~2-4s/sec of audio (CPU) |
| medium | 769M | 2-3s | ~3-6s/sec of audio (CPU) |
| large | 1550M | 3-5s | ~5-10s/sec of audio (CPU) |

On GPU (NVIDIA CUDA), inference is typically **5-20x faster** depending on hardware.

## Multi-Model Support

WhisperClient loads one model at a time. For multi-model scenarios:

```csharp
// Create separate clients for different models
using var enClient = await WhisperClient.CreateAsync(new WhisperOptions
{
    Model = KnownWhisperModels.WhisperSmallEn
});

using var multiClient = await WhisperClient.CreateAsync(new WhisperOptions
{
    Model = KnownWhisperModels.WhisperMedium
});

// Use as needed
var result1 = await enClient.TranscribeAsync("english.wav");
var result2 = await multiClient.TranscribeAsync("spanish.wav");
```

**Note:** Loading multiple models simultaneously requires significant RAM. Use sequentially when possible.

## Dependency Injection

The `AddWhisper()` extension registers a single WhisperClient in the service container:

```csharp
builder.Services.AddWhisper(options => { ... });
```

This creates a singleton instance shared across the application, minimizing memory usage.

## Error Handling

Error scenarios and recovery:

| Scenario | Exception | Recovery |
|----------|-----------|----------|
| Audio file missing | FileNotFoundException | Check file path |
| Model download timeout | HttpRequestException | Retry or check internet |
| Insufficient memory | OutOfMemoryException | Use smaller model |
| Invalid audio format | InvalidOperationException | Convert to WAV/MP3 |
| ONNX Runtime not found | DllNotFoundException | Reinstall package |

## Threading & Concurrency

WhisperClient is **not thread-safe**. Use one client per thread or use locks:

```csharp
// Safe: one client per async task
var result = await whisperClient.TranscribeAsync("audio1.wav");

// NOT safe: concurrent access to same client
Task.Run(() => client.TranscribeAsync("audio1.wav"));
Task.Run(() => client.TranscribeAsync("audio2.wav"));  // Race condition!

// Safe with lock
private static readonly SemaphoreSlim s_semaphore = new(1);
public async Task<string> SafeTranscribe(string file)
{
    await s_semaphore.WaitAsync();
    try
    {
        return (await client.TranscribeAsync(file)).Text;
    }
    finally
    {
        s_semaphore.Release();
    }
}
```

## Memory Usage

Approximate memory consumption:

| Model | Loaded Size | Peak During Inference |
|-------|-------------|----------------------|
| tiny.en | ~200 MB | ~400 MB |
| base.en | ~300 MB | ~600 MB |
| small.en | ~1 GB | ~2 GB |
| medium | ~2.5 GB | ~4 GB |
| large | ~5 GB | ~8 GB |

---

## Related Libraries

- **ElBruno.HuggingFace** — Model discovery and download utilities
- **Microsoft.ML.OnnxRuntime** — ONNX model execution
- **OpenAI Whisper** — Original model architecture (https://github.com/openai/whisper)
