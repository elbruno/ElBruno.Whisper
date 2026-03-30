# Squad Decisions

## Active Decisions

### 2025-01-20T16:00:00Z: Architecture decision — ONNX Runtime choice
**By:** Dallas (Lead)

**What:** Using Microsoft.ML.OnnxRuntime (not OnnxRuntimeGenAI) for Whisper inference because Whisper is an encoder-decoder model requiring separate encoder/decoder ONNX sessions. GenAI is designed for autoregressive LLMs. Using onnx-community Whisper ONNX models from HuggingFace which come pre-exported with encoder_model.onnx and decoder_model_merged.onnx.

**Why:** Correct technical choice for Whisper's architecture. GenAI would not work.

---

### 2025-01-20T16:00:01Z: Architecture decision — Model source
**By:** Dallas (Lead)

**What:** Using onnx-community Whisper models from HuggingFace (e.g., onnx-community/whisper-tiny, whisper-base, whisper-small, whisper-medium, whisper-large-v3, whisper-large-v3-turbo). These repos contain pre-exported ONNX models with encoder, decoder, and all required config/tokenizer files.

**Why:** Avoids needing to convert models ourselves. The onnx-community repos are maintained and widely used.

---

### 2025-06-08: Core Library Architecture for ElBruno.Whisper
**By:** Ripley (Backend Dev)

**Status:** Implemented

**Context:** Implementing the complete core library for ElBruno.Whisper, a pure C# speech-to-text library using ONNX Runtime and Whisper models from HuggingFace.

**Decision:**

**Model Management**
- WhisperModelDefinition: Record type with HuggingFaceRepoId, RequiredFiles, Size, language flags, encoder/decoder dimensions
- KnownWhisperModels: Static class with 10 pre-defined models from onnx-community (Tiny/Base/Small/Medium/Large/LargeTurbo, English + Multilingual variants)
- Default Model: WhisperTinyEn (~75MB) for fastest onboarding
- Cache Location: `%LOCALAPPDATA%/ElBruno/Whisper/models` (Windows)

**Audio Processing (Pure C#)**
- WavReader: Parse 16-bit PCM WAV files, support mono/stereo, normalize to [-1, 1] float
- Resampling: Linear interpolation to 16kHz target
- MelSpectrogramProcessor: STFT with 400-sample FFT (25ms), 160-sample hop (10ms), Hanning window with custom Cooley-Tukey radix-2 FFT implementation (no external deps), 80 mel bins with triangular filterbank, log transform for final features, output [1, 80, 3000] tensor (30 seconds, padded/truncated)

**Tokenization**
- WhisperTokenizer: Load vocab from tokenizer.json, decode token IDs to text
- Handle special tokens: `<|startoftranscript|>`, `<|transcribe|>`, `<|translate|>`, `<|notimestamps|>`, language codes
- GPT-2 style byte-pair encoding cleanup (Ġ → space)

**Inference Pipeline**
- WhisperInferenceSession: ONNX Runtime with Microsoft.ML.OnnxRuntime (NOT GenAI)
- Encoder: mel spectrogram → hidden states
- Decoder: Autoregressive token generation with greedy decoding (argmax), past key-value caching via decoder_model_merged.onnx
- Stop conditions: `<|endoftext|>` token or max_tokens (default 448)

**Public API**
- WhisperClient: Main entry point with static `CreateAsync()` factory
- TranscribeAsync: Accept file path or stream, return TranscriptionResult (text, detected language, duration)
- WhisperOptions: Configuration (model, cache dir, language, translate flag, max_tokens, temperature)
- DI Extensions: `AddWhisper()` for IServiceCollection

**Rationale:**
1. Pure C# audio processing eliminates native library dependencies (NAudio, FFmpeg), improves portability and reduces installation friction
2. ONNX Runtime is industry standard, well-maintained, excellent performance
3. HuggingFace downloader integration reuses existing pattern from LocalLLMs, automatic model management
4. Static factory pattern enables async initialization (download + load) without constructor complexity
5. Greedy decoding default is fastest, most deterministic, suitable for production transcription
6. WhisperTinyEn default (75MB model) enables 5-minute quick start experience

**Consequences:**
- Positive: Zero native dependencies for audio processing, consistent API design across ElBruno libraries, automatic model download with progress reporting, DI-friendly architecture, multi-target net8.0 and net10.0 for broad compatibility
- Negative: Custom FFT implementation may be slower than optimized libraries (trade-off for portability), initial model download required (mitigated by small default model + progress reporting)

**Alternatives Considered:**
1. NAudio for WAV processing — Rejected due to native dependency
2. FFmpeg for audio — Rejected due to external binary requirement
3. GenAI ONNX API — Rejected as it's focused on LLMs, not speech models
4. Temperature-based sampling — Deferred to post-v1 (greedy decoding sufficient for MVP)

---

### 2025-07-14: Merged Decoder KV Cache via Runtime Metadata Discovery
**By:** Ripley (Backend Dev)

**Status:** Implemented

**Issue:** #1

**Context:** The Optimum-style `decoder_model_merged.onnx` from `onnx-community/whisper-*` requires a `use_cache_branch` boolean input and past key-value cache management. Without these, the decoder fails at runtime.

**Decision:** Discover past_key_values cache slots dynamically from `InferenceSession.InputMetadata` at construction time, rather than hardcoding layer counts or head dimensions. This makes the implementation work across all Whisper model sizes (tiny through large-v3) without any model-specific code paths.

**Key patterns:**
- **Metadata discovery**: Filter `InputMetadata` for `past_key_values.*` keys, map to `present.*` output names
- **Zero tensors on first step**: Clone metadata shape, replace dynamic dims with batch=1, seq=0
- **Cache cycling**: `present.*` outputs → copy data/shape → feed as `past_key_values.*` inputs next step
- **Backward compatibility**: `_hasCacheBranch` flag gates the `use_cache_branch` input for non-merged models

**Consequences:**
- **Positive**: Works across all 10 known Whisper model sizes without changes. No hardcoded layer/head counts.
- **Positive**: Significant performance improvement — cached steps only pass 1 token instead of the full growing sequence.
- **Trade-off**: Each step copies present tensor data to float[] arrays. Could be optimized with OrtValue pinning in the future.

---

### 2025-07-14: ONNX tensor inputs must match expected rank exactly
**By:** Ripley (Backend Dev)

**Status:** Implemented

**Issue:** #3

**Context:** The `use_cache_branch` input for the merged decoder was created as a scalar (rank 0) tensor using `new int[0]` as the shape. ONNX Runtime strictly validates tensor rank and the model declares this input as rank 1 (shape [1]).

**Decision:** Always create ONNX tensor inputs with the exact rank the model expects. For single-element 1D inputs, use `new[] { 1 }` as the shape — never `new int[0]`.

**Consequence:** This is a general rule for all future ONNX input construction in the inference pipeline.

---

### 2025-06-XX: Test Strategy for Inference and Client Coverage
**By:** Lambert (Tester)

**Status:** Implemented

**Context:** Expanded test coverage to include WhisperInferenceSession, AudioProcessor, WhisperClient, and full integration transcription.

**Decision:**

1. **Internal types tested directly** — leveraged existing `InternalsVisibleTo` in csproj rather than testing only through `WhisperClient`. This gives fine-grained failure isolation.

2. **Integration tests separated** — marked with `[Trait("Category", "Integration")]` so CI can exclude model-download tests with `--filter "Category!=Integration"`. Each has a 5-minute timeout.

3. **AudioProcessor tested with real WAV files** — used `[Theory]` with all 3 test audio files to verify consistent 240,000-element output, finite values, and file/stream equivalence.

4. **WhisperClient tested via failure modes** — since `CreateAsync` requires real model files, unit tests verify exception behavior with non-existent paths rather than happy-path transcription (that's what integration tests are for).

**Consequences:**
- Total non-integration tests: 109 (was 77)
- All original tests still pass
- New files: `Inference/WhisperInferenceSessionTests.cs`, `Audio/AudioProcessorTests.cs`, `WhisperClientTests.cs`, `Integration/WhisperTranscriptionTests.cs`

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
