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

### 2025-07-14: Handle Zero-Dimension Cache Tensors in ONNX Inference
**Author:** Ripley (Backend Dev)  
**Status:** Implemented (PR #9)  
**Related Issue:** #7

**Context:**  
The onnx-community Whisper model exports encoder cross-attention cache tensors with a 0-length dimension. This is a known bug in the model export process. When `WhisperInferenceSession` initializes the KV cache for the first decoder step, it reads the tensor shape from the model metadata. The `AddCacheInputs()` method normalized dynamic dimensions (negative values like `-1`) with `1`, but left explicit `0` dimensions unchanged, producing invalid shapes like `{6,0,64}` that fail ONNX reshape operations.

**Decision:**  
Extended dimension normalization to treat zero-length dimensions the same as dynamic dimensions in `AddCacheInputs()`:
- Changed: `if (shape[d] < 0)` → `if (shape[d] <= 0)`

**Rationale:**
1. **Consistency:** `ExtractPresentOutputs()` already handled zero-dimension batch sizes (`dims[0] == 0`)
2. **Safety:** ONNX tensors should never have 0-length dimensions; graph validation happens regardless
3. **Low Risk:** Dummy cache data (~36KB) is overwritten on first decoder step
4. **Model Compatibility:** Works around onnx-community export bug without model regeneration

**Consequences:**
- All 218 unit tests pass
- Handles both dynamic and zero dimensions
- Code remains safe if future model versions fix the export bug
- Future maintainers have clear documentation of both cases

---

### 2025-07-14: Blazor JS Interop — Uint8Array for byte[] Parameters
**Author:** Ripley (Backend Dev)  
**Status:** Implemented

**Context:**  
BlazorWhisper's "Stop Recording" button silently failed. The `audioRecorder.js` wrapped WAV data with `Array.from(wavBytes)` before passing to `invokeMethodAsync`. This converted `Uint8Array` to a plain JS array `[82, 73, 70, ...]`, which `System.Text.Json` cannot deserialize to `byte[]` (expects base64 string). Errors were swallowed because calls weren't `await`ed.

**Decision:**  
When passing binary data from JavaScript to Blazor C# methods expecting `byte[]`:
1. **Always pass `Uint8Array` directly** — never wrap with `Array.from()`
2. **Always `await` `invokeMethodAsync`** calls so errors surface in `try/catch`

**Files Changed:**
- `src/samples/BlazorWhisper/wwwroot/js/audioRecorder.js` — 3 call sites fixed

**Consequences:**
- Stop Recording now delivers WAV data correctly to transcription pipeline
- Realtime recording chunks fixed
- JS→C# interop errors now logged instead of silently swallowed

---

### 2025-07-14: Mel Spectrogram Must Match OpenAI Whisper Reference
**Author:** Ripley (Backend Dev)  
**Status:** Implemented  
**Affects:** Core audio processing pipeline

**Context:**  
ElBruno.Whisper produced wrong transcriptions for all audio because mel spectrogram preprocessing didn't match OpenAI Whisper's reference implementation (`whisper/audio.py`).

**Decision:**  
Mel spectrogram pipeline MUST exactly match OpenAI Whisper preprocessing:
1. **Power spectrum** (not magnitude): `abs(stft) ** 2`
2. **Log base 10** (not natural log): `log10()`
3. **Dynamic range compression**: `max(log_spec, max_value - 8.0)`
4. **Normalization**: `(log_spec + 4.0) / 4.0`
5. **Periodic Hann window**: Formula `2πi/N` not `2πi/(N-1)`
6. **Padding value**: `0.0f` for normalized silence

**Rationale:**  
- Whisper models trained on specific mel spectrogram values
- Any deviation causes degraded or completely wrong transcriptions
- Reference implementation in PyTorch is ground truth
- ONNX models expect exact preprocessing steps

**Implementation:**
- `src/ElBruno.Whisper/Audio/MelSpectrogramProcessor.cs` — Fixed power, log10, normalization, Hann window
- `src/ElBruno.Whisper/Audio/AudioProcessor.cs` — Fixed padding value
- All 37 audio tests pass; mel values in correct range

**Consequences:**
- ✅ Transcriptions now accurate (when ONNX models correct)
- ✅ Matches OpenAI Whisper standard
- ⚠️ Integration tests revealed ONNX decoder reshape errors (separate issue, now fixed in PR #9)

---

### 2025-07-14: .NET Aspire Orchestration for BlazorWhisper
**Author:** Ripley (Backend Dev)  
**Status:** Implemented

**Context:**  
Bruno wanted observability for BlazorWhisper after WAV upload crash (fixed by Issue #7). Aspire provides dashboard with distributed tracing, logging, health checks.

**Decision:**  
Added .NET Aspire orchestration to BlazorWhisper sample:
1. **BlazorWhisper.AppHost** — Aspire AppHost (Aspire.AppHost.Sdk/13.1.3, net10.0)
2. **BlazorWhisper.ServiceDefaults** — OpenTelemetry, health checks, resilience
3. **BlazorWhisper upgraded to net10.0** — Required for ServiceDefaults compatibility (library already supports net10.0)

**Key Details:**
- AppHost entry point is `AppHost.cs` (Aspire template convention)
- ServiceDefaults uses OpenTelemetry v1.14.0
- Health endpoints (`/health`, `/alive`) only in Development
- BlazorWhisper can still run standalone without AppHost

**Consequences:**
- ✅ Full observability via Aspire dashboard for debugging
- ✅ Standard resilience patterns ready for future services
- ⚠️ BlazorWhisper now targets net10.0 (acceptable, SDK available)

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
