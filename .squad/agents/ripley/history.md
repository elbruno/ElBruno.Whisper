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
