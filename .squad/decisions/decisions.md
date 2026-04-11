# ElBruno.Whisper — Team Decisions

## Decision: Mel Spectrogram Computation Must Match OpenAI Whisper Reference

**Date:** 2025-07-14  
**Author:** Ripley (Backend Dev)  
**Status:** Implemented  
**Issue:** #5

### Context

Transcription was producing completely wrong text because the mel spectrogram computation differed from OpenAI's Whisper reference implementation in critical ways. The model received spectrogram values in a completely wrong numerical range.

### Decision

We have aligned the mel spectrogram computation to exactly match OpenAI Whisper's reference implementation:

1. **Power Spectrum:** Use squared magnitudes (`m * m`) instead of raw magnitudes. This matches Whisper's `abs() ** 2` operation.

2. **Log Base:** Use `log10` instead of natural log (`ln`). This affects the numerical scale significantly.

3. **Whisper Normalization:** Apply the exact normalization Whisper uses:
   - Find max value in the spectrogram
   - Clamp all values to within 8.0 of max
   - Scale with `(val + 4.0) / 4.0` to produce values in approximately [-1, 1] range

4. **Audio Pre-padding:** Pad audio to exactly 30 seconds (480,000 samples) BEFORE STFT computation. This ensures STFT produces exactly 3000 frames, matching Python Whisper's behavior.

5. **Padding Value:** Use `0.0f` (silence) instead of `1e-10f` for mel spectrogram padding, since normalization already brings values to ~[-1, 1].

6. **Audio Duration:** Return actual audio duration from AudioProcessor instead of wall-clock processing time. This gives users accurate audio length, not inference speed.

### Rationale

- **Numerical Compatibility:** ONNX models are exported from Python Whisper and expect inputs in specific numerical ranges. Deviating from reference implementation causes model misbehavior.
- **Power Spectrum:** Energy representation (squared) is standard for spectrograms and matches what the model was trained on.
- **Log10:** Decibel scale uses log10 by convention, and this is what Whisper expects.
- **Normalization:** Whisper's normalization bounds dynamic range and prevents extreme values, making the model more robust.
- **Pre-padding:** Ensures consistent tensor shapes (3000 frames) regardless of audio length, simplifying inference code and matching training data.
- **Audio Duration:** Users care about actual audio length for billing, progress bars, etc. — not how fast we processed it.

### Implementation

- `MelSpectrogramProcessor.cs`: Updated `ComputeMagnitude()` and `ComputeMelSpectrogram()` with power spectrum, log10, and normalization
- `AudioProcessor.cs`: Pre-pad audio to 30 seconds, return tuple with audio duration, use 0.0f padding
- `WhisperClient.cs`: Use audio duration from AudioProcessor instead of wall-clock time
- All tests updated to handle tuple return values
- New test `ProcessAudioFile_ReturnsPositiveAudioDuration` validates duration computation

### Impact

- **Correctness:** Transcription now produces accurate text matching Python Whisper output
- **Breaking Change:** AudioProcessor methods now return `(float[] MelSpectrogram, TimeSpan AudioDuration)` tuples instead of `float[]`
- **Test Coverage:** All 224 tests passing (112 per framework)

### Alternatives Considered

1. **Keep natural log:** Would maintain backward compatibility but produce wrong results. Rejected — correctness is paramount.
2. **Optional normalization:** Would allow users to disable it. Rejected — the model expects normalized inputs, making it optional would cause confusion.
3. **Post-pad audio (after STFT):** Would preserve exact audio duration in STFT. Rejected — Whisper reference pre-pads, and we must match exactly.

### References

- Issue #5: Transcription produces wrong text
- OpenAI Whisper reference implementation: https://github.com/openai/whisper
- Key files: `src/ElBruno.Whisper/Audio/MelSpectrogramProcessor.cs`, `src/ElBruno.Whisper/Audio/AudioProcessor.cs`

---

## Decision: Shared test audio assets at repo root

**Date:** 2025-07-14  
**Author:** Ripley (Backend Dev)  
**Status:** Implemented

### Context

Test WAV files lived inside the test project at `src/tests/ElBruno.Whisper.Tests/TestData/`. Bruno requested better discoverability and cross-project reuse. Test data assets aren't source code, so living outside `src/` is appropriate.

### Decision

Relocated all test audio files to `testdata/audio/` at repository root. The test `.csproj` uses `<Content Include>` with `<Link>` to map them into the `TestData\` output folder, so test code paths are unchanged.

### Structure

```
testdata/
  audio/
    test-audio-small.wav    (201 KB)
    test-audio-medium.wav   (347 KB)
    test-audio-failing.wav  (345 KB) — triggered Issue #7
```

### Convention

- Future test projects should reference `testdata/audio/` instead of duplicating WAV files
- Use `<Content Include>` with `<Link>` in .csproj to map shared assets into project-local output paths
- `git mv` preserves rename history for binary assets

### Consequences

- ✅ Audio assets discoverable at repo root
- ✅ Any future test project can reference the same files
- ✅ All 109 tests pass, zero code changes needed
- ✅ Git history preserved via rename detection

---

## Decision: Test Audio Documentation

**Date:** 2025-01-XX  
**Author:** Ash (DevRel)  
**Status:** Complete

### Overview

Created comprehensive documentation for the test audio files moved to `testdata/audio/` directory. This makes the testing resources discoverable and reusable by other libraries and applications.

### Deliverables

#### 1. `testdata/audio/README.md`
Complete reference guide for test audio files:
- **File inventory table** with sizes, durations, descriptions, model compatibility
- **Model notes** explaining why tiny model returns empty text for medium file
- **C# usage examples** for unit and integration tests
- **Test coverage breakdown** showing which test classes use these files
- **Known issues** documenting Issue #7 (ONNX reshape with zero-dimension cache)
- **Attribution and licensing** (MIT, same as project)
- **References** to related code and issues

**Key Technical Details:**
- test-audio-small.wav (201 KB, ~2-3 sec) — Works with all models
- test-audio-medium.wav (347 KB, ~4-5 sec) — Tiny model may return empty text (pre-existing limitation)
- test-audio-failing.wav (345 KB, ~4-5 sec) — Previously crashed ONNX reshape, now works

#### 2. `docs/testing.md`
Testing guide explaining test organization and execution:
- **Test categories** (unit, integration, samples)
- **Test data structure** (how .csproj copies files to TestData/)
- **Running tests** with filter commands:
  - `dotnet test --filter "Category!=Integration"` — fast unit tests
  - `dotnet test --filter "Category=Integration"` — full integration with models
- **CI/CD pipeline** explanation and link to workflow files
- **Troubleshooting** (TestData paths, model downloads, memory)
- **Test templates** for writing new unit and integration tests
- **Test statistics** (218 tests: 109 unit passing, ~18 integration)

#### 3. Updated `README.md`
- Added "Testing" section with quick test commands
- Added links to test audio files and testing guide in Documentation section
- Changed build command to use `--filter "Category!=Integration"` for faster CI

### Verification

✅ Build succeeds (Release configuration)  
✅ 109 unit tests pass (all non-integration tests)  
✅ Documentation links are valid and discoverable  
✅ Test file copying verified in .csproj (content link to testdata/audio/*)  
✅ All three audio files accessible: test-audio-small.wav, test-audio-medium.wav, test-audio-failing.wav

### Design Decisions

1. **Separate documentation file per directory**
    - `testdata/audio/README.md` — Audio-specific (what they are, compatibility, usage examples)
    - `docs/testing.md` — General testing guide (how to run tests, organization, CI/CD)
    - This matches ElBruno convention of putting docs/README.md in each major directory

2. **Honest about model limitations**
    - Documented that tiny model may return empty text for test-audio-medium.wav
    - Explained this is a pre-existing limitation of the tiny model, not a library bug
    - Provided clear path to use larger models for better results

3. **Explicit Issue #7 reference**
    - Documented the ONNX reshape error fix (zero-dimension cache tensors)
    - Explained why test-audio-failing.wav exists (edge case validation)
    - Links to PR #9 for readers interested in the fix

4. **Discoverable from main README**
    - Added "Testing" section showing quick test commands
    - Added test documentation links in Documentation section
    - Lowercase "testing" in section name for consistency with other docs

### Future Enhancements

- If more test audio files are added (e.g., multilingual samples), expand the table
- If integration tests are reorganized, update running-tests section
- Add performance benchmarks for different models in the testing guide
- Consider adding audio file generation script if test coverage expands

### References

- Related issues: #7 (ONNX reshape error)
- Related PRs: #9 (Fix zero-dimension tensor handling)
- Test locations: `src/tests/ElBruno.Whisper.Tests/Audio/`, `src/tests/ElBruno.Whisper.Tests/Integration/`

---

## Decision: .NET Aspire Orchestration for BlazorWhisper

**By:** Ripley (Backend Dev)  
**Date:** 2025-07-14  
**Status:** Implemented

### Context

Bruno wanted better observability for the BlazorWhisper sample app after a WAV upload crash (caused by the ONNX empty cache tensor bug, now fixed). Aspire provides a dashboard with distributed tracing, logging, and health checks out of the box.

### Decision

Added .NET Aspire orchestration to the BlazorWhisper sample:

1. **BlazorWhisper.AppHost** — Aspire AppHost (Aspire.AppHost.Sdk/13.1.3, net10.0) that orchestrates the Blazor app
2. **BlazorWhisper.ServiceDefaults** — Shared project with OpenTelemetry, health checks, resilience, and service discovery
3. **BlazorWhisper upgraded to net10.0** — Required for ServiceDefaults compatibility (the library already supports net10.0)

### Key Details

- AppHost entry point is `AppHost.cs` (Aspire template convention), not `Program.cs`
- ServiceDefaults uses OpenTelemetry packages v1.14.0 for traces/metrics/logs
- Health endpoints (`/health`, `/alive`) only exposed in Development environment
- BlazorWhisper can still run standalone without the AppHost

### Consequences

- **Positive:** Full observability via Aspire dashboard (traces, logs, metrics, health) for debugging transcription issues
- **Positive:** Standard resilience and service discovery patterns ready if more services are added
- **Trade-off:** BlazorWhisper now targets net10.0 instead of net8.0 (acceptable since .NET 10 SDK is available)
