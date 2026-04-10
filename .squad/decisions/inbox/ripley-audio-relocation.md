# Decision: Shared test audio assets at repo root

**Date:** 2025-07-14  
**Author:** Ripley (Backend Dev)  
**Status:** Implemented

## Context

Test WAV files lived inside the test project at `src/tests/ElBruno.Whisper.Tests/TestData/`. Bruno requested better discoverability and cross-project reuse. Test data assets aren't source code, so living outside `src/` is appropriate.

## Decision

Relocated all test audio files to `testdata/audio/` at repository root. The test `.csproj` uses `<Content Include>` with `<Link>` to map them into the `TestData\` output folder, so test code paths are unchanged.

## Structure

```
testdata/
  audio/
    test-audio-small.wav    (201 KB)
    test-audio-medium.wav   (347 KB)
    test-audio-failing.wav  (345 KB) — triggered Issue #7
```

## Convention

- Future test projects should reference `testdata/audio/` instead of duplicating WAV files
- Use `<Content Include>` with `<Link>` in .csproj to map shared assets into project-local output paths
- `git mv` preserves rename history for binary assets

## Consequences

- ✅ Audio assets discoverable at repo root
- ✅ Any future test project can reference the same files
- ✅ All 109 tests pass, zero code changes needed
- ✅ Git history preserved via rename detection
