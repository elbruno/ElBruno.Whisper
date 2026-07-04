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

## Rolling Incremental Transcription

`GetStreamingTextAsync()` layers a rolling-window coordinator on top of the existing full-window Whisper inference path:

```text
Normalized audio
  ├─ rolling window slice
  ├─ mel spectrogram
  ├─ Whisper inference
  ├─ text hypothesis
  ├─ local agreement / overlap merge
  └─> StreamingTranscriptionUpdate
       ├─ CommittedText
       ├─ ProvisionalText
       └─ IsFinal
```

### How it works

1. Audio is normalized once to 16 kHz mono.
2. The client reuses overlapping windows of that normalized audio.
3. Each window is transcribed through the same backend as `TranscribeAsync()`.
4. Neighboring hypotheses are merged with suffix/prefix overlap checks.
5. Stable text moves into `CommittedText`; the newest revisable tail stays in `ProvisionalText`.
6. The final update flushes the remaining provisional text exactly once.

### Limitations

- Whisper is not a transducer-style streaming model, so updates are approximate.
- Provisional text may be revised by later windows.
- The API operates on completed file or stream content rather than an endless live microphone feed.

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
| Invalid audio format | WhisperAudioFormatException | Supply a valid WAV stream or the correct `WhisperAudioFormat` for raw PCM |
| ONNX Runtime not found | DllNotFoundException | Reinstall package |

The raw PCM path keeps caller-owned streams open, avoids temporary files, and uses pooled read buffers before resampling into Whisper's 16 kHz mono format.

## Threading & Concurrency

`WhisperClient` is thread-safe for concurrent transcription calls. The concurrency contract is defined by `WhisperOptions.Concurrency`:

- `MaximumConcurrentRequests` limits how many requests can run inference at once.
- `QueueTimeout` bounds how long callers wait for a free slot before `TimeoutException` is thrown.
- `EnableSessionPooling` decides whether finished requests return their ONNX session to a pool or dispose it immediately.

### Runtime Layout

The client now separates immutable shared model resources from per-request state:

```text
WhisperClient
  ├─ AudioProcessor (shared)
  ├─ WhisperModelRuntime (shared)
  │   ├─ WhisperTokenizer (shared)
  │   ├─ Suppress-token config (shared)
  │   └─ WhisperSessionPool (shared)
  │       └─ WhisperInferenceSession (leased per request)
  └─ Request state
      ├─ mel spectrogram
      ├─ token list
      ├─ KV cache
      └─ transcription result
```

### Concurrency Behavior

With the default settings, requests are serialized through a single inference slot. Raising the limit allows one shared client to process multiple files at the same time:

```csharp
using var client = await WhisperClient.CreateAsync(new WhisperOptions
{
    Concurrency = new WhisperConcurrencyOptions
    {
        MaximumConcurrentRequests = 2,
        QueueTimeout = TimeSpan.FromSeconds(15),
        EnableSessionPooling = true
    }
});

var results = await Task.WhenAll(
    client.TranscribeAsync("audio1.wav"),
    client.TranscribeAsync("audio2.wav"));
```

### Cancellation and Metrics

Requests observe cancellation at these safe points:

1. Before audio processing starts
2. After audio processing completes
3. While waiting for an inference slot
4. At each decoder step during token generation

The library also publishes `System.Diagnostics.Metrics` histograms through the `ElBruno.Whisper` meter:

- `elbruno.whisper.queue.wait.duration`
- `elbruno.whisper.inference.duration`

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
