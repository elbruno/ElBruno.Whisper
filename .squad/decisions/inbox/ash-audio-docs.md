# Test Audio Documentation

**Date:** 2025-01-XX  
**Author:** Ash (DevRel)  
**Status:** Complete

## Overview

Created comprehensive documentation for the test audio files moved to `testdata/audio/` directory. This makes the testing resources discoverable and reusable by other libraries and applications.

## Deliverables

### 1. `testdata/audio/README.md`
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

### 2. `docs/testing.md`
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

### 3. Updated `README.md`
- Added "Testing" section with quick test commands
- Added links to test audio files and testing guide in Documentation section
- Changed build command to use `--filter "Category!=Integration"` for faster CI

## Verification

✅ Build succeeds (Release configuration)  
✅ 109 unit tests pass (all non-integration tests)  
✅ Documentation links are valid and discoverable  
✅ Test file copying verified in .csproj (content link to testdata/audio/*)  
✅ All three audio files accessible: test-audio-small.wav, test-audio-medium.wav, test-audio-failing.wav

## Design Decisions

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

## Future Enhancements

- If more test audio files are added (e.g., multilingual samples), expand the table
- If integration tests are reorganized, update running-tests section
- Add performance benchmarks for different models in the testing guide
- Consider adding audio file generation script if test coverage expands

## References

- Related issues: #7 (ONNX reshape error)
- Related PRs: #9 (Fix zero-dimension tensor handling)
- Test locations: `src/tests/ElBruno.Whisper.Tests/Audio/`, `src/tests/ElBruno.Whisper.Tests/Integration/`
