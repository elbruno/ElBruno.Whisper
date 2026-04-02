# Decision: Mel Spectrogram Computation Must Match OpenAI Whisper Reference

**Date:** 2025-07-14  
**Author:** Ripley (Backend Dev)  
**Status:** Implemented  
**Issue:** #5

## Context

Transcription was producing completely wrong text because the mel spectrogram computation differed from OpenAI's Whisper reference implementation in critical ways. The model received spectrogram values in a completely wrong numerical range.

## Decision

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

## Rationale

- **Numerical Compatibility:** ONNX models are exported from Python Whisper and expect inputs in specific numerical ranges. Deviating from reference implementation causes model misbehavior.
- **Power Spectrum:** Energy representation (squared) is standard for spectrograms and matches what the model was trained on.
- **Log10:** Decibel scale uses log10 by convention, and this is what Whisper expects.
- **Normalization:** Whisper's normalization bounds dynamic range and prevents extreme values, making the model more robust.
- **Pre-padding:** Ensures consistent tensor shapes (3000 frames) regardless of audio length, simplifying inference code and matching training data.
- **Audio Duration:** Users care about actual audio length for billing, progress bars, etc. — not how fast we processed it.

## Implementation

- `MelSpectrogramProcessor.cs`: Updated `ComputeMagnitude()` and `ComputeMelSpectrogram()` with power spectrum, log10, and normalization
- `AudioProcessor.cs`: Pre-pad audio to 30 seconds, return tuple with audio duration, use 0.0f padding
- `WhisperClient.cs`: Use audio duration from AudioProcessor instead of wall-clock time
- All tests updated to handle tuple return values
- New test `ProcessAudioFile_ReturnsPositiveAudioDuration` validates duration computation

## Impact

- **Correctness:** Transcription now produces accurate text matching Python Whisper output
- **Breaking Change:** AudioProcessor methods now return `(float[] MelSpectrogram, TimeSpan AudioDuration)` tuples instead of `float[]`
- **Test Coverage:** All 224 tests passing (112 per framework)

## Alternatives Considered

1. **Keep natural log:** Would maintain backward compatibility but produce wrong results. Rejected — correctness is paramount.
2. **Optional normalization:** Would allow users to disable it. Rejected — the model expects normalized inputs, making it optional would cause confusion.
3. **Post-pad audio (after STFT):** Would preserve exact audio duration in STFT. Rejected — Whisper reference pre-pads, and we must match exactly.

## References

- Issue #5: Transcription produces wrong text
- OpenAI Whisper reference implementation: https://github.com/openai/whisper
- Key files: `src/ElBruno.Whisper/Audio/MelSpectrogramProcessor.cs`, `src/ElBruno.Whisper/Audio/AudioProcessor.cs`
