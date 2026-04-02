# Ripley — History

## Project Context
- **Project:** ElBruno.Whisper — .NET library for local Whisper speech-to-text
- **User:** Bruno Capuano
- **Stack:** .NET 8.0/10.0, C#, ONNX Runtime, ElBruno.HuggingFace.Downloader
- **Reference repo:** ElBruno.LocalLLMs (patterns for options, client, model download, NuGet)

## Learnings

## Learnings

### 2026-03-30 12:16 - Core Library Implementation
- Implemented complete ElBruno.Whisper library with 13 core files
- Created model definitions: WhisperModelSize enum, WhisperModelDefinition record, KnownWhisperModels with 10 pre-defined ONNX models from onnx-community
- Implemented pure C# audio processing: WavReader for 16-bit PCM WAV files, AudioProcessor for log-mel spectrogram generation (80 mel bins, 3000 frames = 30s)
- Built MelSpectrogramProcessor with STFT, Hanning window, mel filterbank, and Cooley-Tukey FFT implementation
- Created WhisperTokenizer for decoding model outputs with special token handling (startoftranscript, transcribe, translate, notimestamps)
- Implemented WhisperInferenceSession using Microsoft.ML.OnnxRuntime for encoder-decoder pipeline with autoregressive decoding
- Built WhisperClient with static CreateAsync factory pattern (matches LocalLLMs design)
- Integrated ElBruno.HuggingFace.Downloader for automatic model downloads to %LOCALAPPDATA%/ElBruno/Whisper/models
- Added DI extensions (WhisperServiceExtensions) for easy service registration
- Default model: WhisperTinyEn (~75MB) for fastest quick-start experience
- All implementations follow ElBruno repository conventions: net8.0;net10.0 multi-targeting, proper namespacing, comprehensive XML documentation

### 2025-07-14 - Fix: use_cache_branch + KV Cache for Merged Decoder (Issue #1)
- The Optimum-style `decoder_model_merged.onnx` has an `If` node that requires a `use_cache_branch` boolean scalar input
- First decoder step: `use_cache_branch=false`, full token sequence, empty zero-tensors for past_key_values (shape with seq_len=0)
- Subsequent steps: `use_cache_branch=true`, single last token, feed `present.*` outputs back as `past_key_values.*` inputs
- Cache slot discovery uses `_decoderSession.InputMetadata` at construction time — filters for `past_key_values.*` keys, maps to `present.*` output names. This makes it model-size agnostic.
- Zero tensors for first step: clone metadata shape, replace dynamic dims (-1) with batch=1 and seq=0
- Present outputs extracted via `result.AsTensor<float>().ToArray()` + `.Dimensions.ToArray()` before results disposal
- Constructor now accepts `numDecoderLayers` and `encoderDimension` (passed from WhisperModelDefinition via WhisperClient.CreateAsync)
- `_hasCacheBranch` flag makes the code backward-compatible with models that don't have the merged decoder pattern
- Key files: `src/ElBruno.Whisper/Inference/WhisperInferenceSession.cs`, `src/ElBruno.Whisper/WhisperClient.cs`

### 2025-07-14 - Fix: use_cache_branch tensor rank (Issue #3)
- ONNX Runtime validates tensor rank strictly — `use_cache_branch` must be rank 1 (shape [1]), not rank 0 (scalar)
- Changed `new int[0]` → `new[] { 1 }` in WhisperInferenceSession.cs line 145
- Pattern: when ONNX models declare an input as 1D, always use `new[] { 1 }` shape even for single-element booleans — never `new int[0]`
- Key file: `src/ElBruno.Whisper/Inference/WhisperInferenceSession.cs`

### 2026-03-30 - Cross-team sync: Test expansion and issue fixes
- Lambert completed 32 new tests across 4 files (inference, audio processing, client, integration)
- Total test count: 109 non-integration tests (up from 77)
- Test infrastructure: InternalsVisibleTo enables direct internal type testing, integration traits for CI filtering
- All tests passing; ready for CI/CD pipeline integration
- Key decision documented: Tensor rank must match model declaration for all ONNX input tensors

### 2025-07-14 - Fix: Empty KV cache tensor shapes causing ONNX Reshape crash
- The merged decoder's ONNX `If` node validates shapes for BOTH branches even when `use_cache_branch=false`
- Previous empty cache used `seq=0` → shapes like `[1, 6, 0, 64]` → Reshape node fails on 0-length dims
- Fix: set ALL dynamic dimensions (-1) to 1, allocate `new float[totalElements]` instead of `Array.Empty<float>()`
- Result: shapes like `[1, 6, 1, 64]` — zero-filled data is ignored since `use_cache_branch=false`
- Pattern: ONNX tensors should never have 0-length dimensions, even for "unused" cache inputs — the graph still validates shapes
- Also excluded integration tests from CI/CD via `--filter "Category!=Integration"` in both `ci.yml` and `publish.yml`
- Key files: `src/ElBruno.Whisper/Inference/WhisperInferenceSession.cs`, `.github/workflows/ci.yml`, `.github/workflows/publish.yml`

### 2025-07-14 - .NET Aspire Orchestration for BlazorWhisper
- Added Aspire AppHost (`src/samples/BlazorWhisper.AppHost/`) with `Aspire.AppHost.Sdk/13.1.3` targeting net10.0
- Added ServiceDefaults (`src/samples/BlazorWhisper.ServiceDefaults/`) with OpenTelemetry, health checks, resilience, and service discovery
- Upgraded BlazorWhisper from net8.0 → net10.0 (required for ServiceDefaults compatibility; library already multi-targets net8.0;net10.0)
- Wired `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` in BlazorWhisper's Program.cs
- AppHost registers BlazorWhisper as `"blazor-whisper"` project resource
- Aspire templates generate into `AppHost.cs` (not `Program.cs`) — the entry point file is `AppHost.cs`
- Updated `ElBruno.Whisper.slnx` with both new projects under `/src/samples/`
- Pattern: Aspire workload not required if templates are already installed; `dotnet new list --tag aspire` confirms availability
- Key files: `src/samples/BlazorWhisper.AppHost/`, `src/samples/BlazorWhisper.ServiceDefaults/`, `src/samples/BlazorWhisper/Program.cs`

### 2025-07-14 - Fix: Mel Spectrogram Computation to Match Whisper Reference (Issue #5)
- **Root cause:** Mel spectrogram values were in completely wrong numerical range, causing transcription failures
- Fixed power spectrum: changed from raw magnitude to squared magnitude (`m * m`) in `ComputeMagnitude()` — matches OpenAI Whisper's `abs() ** 2`
- Fixed log transform: changed from natural log (`MathF.Log`) to log10 (`MathF.Log10`) in `ComputeMelSpectrogram()`
- Added Whisper normalization: clamp values to within 8.0 of max, then scale with `(val + 4.0f) / 4.0f` → produces ~[-1, 1] range
- Pre-pad audio to exactly 30 seconds (480,000 samples) BEFORE STFT computation — ensures STFT produces exactly 3000 frames, matching Python Whisper
- Fixed padding value in mel domain from `1e-10f` (log of near-zero) to `0.0f` (silence after normalization)
- Changed AudioProcessor return type to `(float[] MelSpectrogram, TimeSpan AudioDuration)` tuple — reports actual audio duration, not wall-clock processing time
- Updated WhisperClient to use audio duration from AudioProcessor instead of `DateTime.UtcNow` timing
- Updated all AudioProcessorTests to handle tuple return values with `var (result, _)` pattern
- Added `ProcessAudioFile_ReturnsPositiveAudioDuration` test to validate audio duration computation
- Updated MelSpectrogramTests comment: silent audio values are now in [-1, 1] range after normalization (not log-dominated -23)
- All 224 tests passing (112 per target framework: net8.0 + net10.0)
- Key files: `src/ElBruno.Whisper/Audio/MelSpectrogramProcessor.cs`, `src/ElBruno.Whisper/Audio/AudioProcessor.cs`, `src/ElBruno.Whisper/WhisperClient.cs`, test files
