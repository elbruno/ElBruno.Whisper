# Decision: Timestamp Support Architecture (Issue #12)

**Author:** Ripley (Backend Dev)
**Date:** 2025-07-15
**Status:** Implemented

## Context
Community member requested timestamp support for Whisper ONNX transcription. Previously all timestamp tokens (50364+) were suppressed.

## Decision
- **Opt-in via `WhisperOptions.EnableTimestamps`** (default false) — preserves backward compatibility
- **No new inference logic** — timestamps are regular tokens the model generates when not suppressed
- **`TranscriptionSegment` as a sealed record** — immutable, matches existing `TranscriptionResult` pattern
- **Nullable `Segments` on `TranscriptionResult`** — null when timestamps disabled, populated list when enabled
- **`BuildResult()` helper** extracted in `WhisperClient` to DRY up file/stream overloads

## Rationale
- Keeping timestamps opt-in means zero risk to existing users
- Whisper natively produces timestamp pairs — we just need to stop suppressing them and parse the output
- Sealed record for segment matches the library's immutability-first API style

## Files Changed
- `src/ElBruno.Whisper/WhisperOptions.cs` — added `EnableTimestamps`
- `src/ElBruno.Whisper/TranscriptionSegment.cs` — new file
- `src/ElBruno.Whisper/TranscriptionResult.cs` — added `Segments`
- `src/ElBruno.Whisper/Tokenizer/WhisperTokenizer.cs` — timestamp methods + `NoTimestampsToken`
- `src/ElBruno.Whisper/WhisperClient.cs` — conditional initial tokens, suppress tokens, BuildResult helper
