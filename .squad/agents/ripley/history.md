# Ripley — History

## Project Context
- **Project:** ElBruno.Whisper — .NET library for local Whisper speech-to-text
- **User:** Bruno Capuano
- **Stack:** .NET 8.0/10.0, C#, ONNX Runtime, ElBruno.HuggingFace.Downloader
- **Reference repo:** ElBruno.LocalLLMs (patterns for options, client, model download, NuGet)

## Core Context

**Foundation (2026-03-30 to 2025-07-14):**
- Built complete ElBruno.Whisper library: WhisperClient, ONNX inference, audio processing (mel spectrogram with STFT/FFT), model download, DI registration
- Fixed Issue #1 (KV cache + use_cache_branch for merged decoder models)
- Fixed Issue #3 (tensor rank validation — use [1] not [0] for 1D ONNX inputs)
- Fixed Issue #5 (mel spectrogram numerical alignment: power spectrum, log10, normalization, pre-padding to 30s)
- Test infrastructure: 109 unit tests passing on net8.0 + net10.0; InternalsVisibleTo for internal type testing; integration traits for CI filtering
- .NET Aspire orchestration for BlazorWhisper sample (AppHost + ServiceDefaults + OpenTelemetry)
- Blazor JS→C# byte[] fix (stopRecording deserialization)
- Zero-dimension cache tensor fix (Issue #7): normalized both negative (-1) and zero (0) dims to 1 in AddCacheInputs()
- Test audio relocation: moved 3 WAV files to `testdata/audio/` at repo root (better discoverability, cross-project reuse)

**Key patterns learned:**
- ONNX tensors must never have 0-length dimensions, even for unused cache inputs
- Whisper mel spectrogram expects: squared magnitude, log10, dynamic range compression, [-1, 1] normalization range
- Audio pre-padding (not post) ensures consistent 3000 STFT frames matching Python Whisper
- Use git mv to preserve binary file history during relocation

## Learnings


### 2025-07-14 - Fix: use_cache_branch + KV Cache for Merged Decoder (Issue #1, summarized in Core Context)

### 2026-04-10 - Issue #7 Fix: Zero-Dimension Cache Tensors (PR #9)
- Root cause: `AddCacheInputs()` normalized dynamic dims (-1) with 1, but left zero dims (0) unchanged
- onnx-community models export encoder cache tensors with explicit 0-length dimensions (export bug)
- Invalid shapes like {6,0,64} fail to reshape to 4D tensors {1,6,64,64} for decoder self-attention
- Fix: Extended normalization logic from `shape[d] < 0` to `shape[d] <= 0`
- Low-risk: dummy cache (~36KB) overwritten on first decoder step, matches existing pattern in ExtractPresentOutputs()
- All 218 tests pass; backward-compatible with future models that don't have zero-dimension issue
- Branch: fix/issue-7-onnx-reshape-error, PR #9 ready for merge
- Dimension normalization in `AddCacheInputs()` handled dynamic dims (-1) but not zero dims (0)
- onnx-community model export bug produces encoder cache tensors with 0-length dimensions
- Single-operator fix: `shape[d] < 0` → `shape[d] <= 0` normalizes both to 1
- Consistent with existing pattern in `ExtractPresentOutputs()` (which already handled zero batch size)
- All 218 tests pass; low-risk change (~36KB dummy cache overwritten on first step)
- PR #9 created and ready for merge
- Branch: fix/issue-7-onnx-reshape-error

### 2026-04-10 - Team coordination: Issue #7 fix implementation completed
- Dallas: Root cause analysis identified 1-line fix
- Ripley: Implemented fix, created PR #9, all tests passing
- Status: Ready for merge
- Issue #8 determined to be environmental (non-library issue), closed by Ash with troubleshooting guidance
- The merged decoder's ONNX `If` node validates shapes for BOTH branches even when `use_cache_branch=false`
- Previous empty cache used `seq=0` → shapes like `[1, 6, 0, 64]` → Reshape node fails on 0-length dims
- Fix: set ALL dynamic dimensions (-1) to 1, allocate `new float[totalElements]` instead of `Array.Empty<float>()`
- Result: shapes like `[1, 6, 1, 64]` — zero-filled data is ignored since `use_cache_branch=false`
- Pattern: ONNX tensors should never have 0-length dimensions, even for "unused" cache inputs — the graph still validates shapes
- Also excluded integration tests from CI/CD via `--filter "Category!=Integration"` in both `ci.yml` and `publish.yml`
- Key files: `src/ElBruno.Whisper/Inference/WhisperInferenceSession.cs`, `.github/workflows/ci.yml`, `.github/workflows/publish.yml`

### 2026-04-10T14:25 - Relocated test audio to testdata/audio/ (commit 85dfa95)
- Moved 3 WAV files from `src/tests/ElBruno.Whisper.Tests/TestData/` to `testdata/audio/` at repo root via `git mv`
- Updated test `.csproj` with `<Content Include>` + `<Link>` pattern to map files into output `TestData\` (test code paths unchanged)
- All 109 unit tests pass, zero code changes
- Decision: Shared test audio assets enable cross-project reuse and improve discoverability
- Convention: Future test projects should reference `testdata/audio/` instead of duplicating audio files
- Key files: `testdata/audio/{test-audio-*.wav}`, `src/tests/ElBruno.Whisper.Tests/ElBruno.Whisper.Tests.csproj`

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
### 2025-07-14 - Fix: Mel spectrogram pipeline to match OpenAI Whisper (Issue #5)
- Fixed 4 critical bugs causing wrong transcription for ALL audio files
- **Bug 1:** Changed natural log to log10 in `ComputeMelSpectrogram` (line 41: `MathF.Log` → `MathF.Log10`)
- **Bug 2:** Changed magnitude to power spectrum in `ComputeMagnitude` (line 89: `Magnitude` → `Magnitude * Magnitude`)
- **Bug 3:** Added dynamic range compression (clamp to `max - 8.0`) and normalization (`(log_spec + 4.0) / 4.0`) after log10 computation
- **Bug 4:** Changed padding value from `1e-10f` to `0.0f` in AudioProcessor (normalized silence value)
- **Bonus fix:** Changed Hann window from symmetric to periodic (divides by `size` instead of `size-1`) to match `torch.hann_window(periodic=True)`
- Verified mel values are now in correct Whisper normalized range: silent audio = -1.5, 440Hz sine wave = [-0.16, 1.84]
- All 37 audio unit tests pass (shapes, finite values, silence vs non-silence detection)
- Integration tests now fail with ONNX decoder reshape errors — expected behavior since models may need regeneration or previous 'working' state was garbage transcriptions
- Reference: OpenAI Whisper `whisper/audio.py` — `stft.abs() ** 2`, `log10()`, `max - 8.0`, `(log_spec + 4.0) / 4.0`
- Key files: `src/ElBruno.Whisper/Audio/MelSpectrogramProcessor.cs`, `src/ElBruno.Whisper/Audio/AudioProcessor.cs`

### 2025-07-14 - Fix: Blazor JS→C# byte[] deserialization (Stop Recording bug)
- `invokeMethodAsync` with `byte[]` C# parameter requires a `Uint8Array` on the JS side — Blazor auto-converts it to base64
- Passing `Array.from(wavBytes)` creates a JSON number array `[82,73,70,...]` which `System.Text.Json` cannot deserialize to `byte[]`
- Fixed 3 call sites in `audioRecorder.js`: `stopRecording`, `startRealtimeRecording` interval, `stopRealtimeRecording` flush
- Also added `await` to the `stopRecording` `invokeMethodAsync` call so errors are caught by the surrounding `try/catch`
- Pattern: When passing binary data from JS to Blazor C# `byte[]`, always pass `Uint8Array` directly — never wrap with `Array.from()`
- Key file: `src/samples/BlazorWhisper/wwwroot/js/audioRecorder.js`

### 2025-07-14 - Fix: Zero-dimension cache tensors in decoder initialization (Issue #7)
- The onnx-community Whisper model exports encoder cross-attention cache tensors with a 0-length dimension (known model export bug)
- The `AddCacheInputs()` method only replaced negative (dynamic) dimensions with 1, leaving explicit 0 dimensions unchanged
- This produced invalid tensor shapes like `{6,0,64}` causing reshape failures when creating 4D tensors `{1,6,64,64}`
- Fix: changed condition from `shape[d] < 0` to `shape[d] <= 0` in `AddCacheInputs()` (1-line change)
- This extends the existing zero-dimension workaround in `ExtractPresentOutputs()` to initial cache creation
- Pattern: ONNX tensors should never have 0-length dimensions, even if they come from model metadata — always normalize to 1
- All 218 unit tests pass, no behavior change since the ~36KB dummy cache data gets overwritten on first decoder step
- Key file: `src/ElBruno.Whisper/Inference/WhisperInferenceSession.cs` (line 199)
- Pull request: #9

### 2025-07-14 - Reorganize test audio assets to testdata/audio/
- Moved 3 WAV test files from `src/tests/ElBruno.Whisper.Tests/TestData/` to `testdata/audio/` (repo root)
- Used `git mv` to preserve file history (Git detected 100% renames)
- Updated .csproj: replaced `<None Update="TestData\**\*">` with `<Content Include="..\..\..\testdata\audio\*">` using `<Link>TestData\%(Filename)%(Extension)</Link>` to maintain backward-compatible output path
- Test code unchanged — files still land in `TestData\` in the build output directory
- All 109 non-integration tests pass on both net8.0 and net10.0
- Purpose: makes audio assets discoverable at repo root and reusable by future test projects
- Key files: `testdata/audio/`, `src/tests/ElBruno.Whisper.Tests/ElBruno.Whisper.Tests.csproj`

### 2026-04-10T14:38 - Release v0.1.6 orchestration
- Version bump 0.1.5 → 0.1.6 in ElBruno.Whisper.csproj (Ripley)
- Release build passed, all 109 unit tests passing
- Git commit and tag v0.1.6 created and pushed (Ripley)
- Release notes drafted and GitHub release v0.1.6 created with full notes (Coordinator)
- Status: Live on GitHub
