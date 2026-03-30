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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
