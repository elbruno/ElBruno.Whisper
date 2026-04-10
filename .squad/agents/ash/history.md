# Ash — History

## Project Context
- **Project:** ElBruno.Whisper — .NET library for local Whisper speech-to-text
- **User:** Bruno Capuano
- **Stack:** .NET 8.0/10.0, C#, ONNX Runtime, ElBruno.HuggingFace.Downloader
- **Reference repo:** ElBruno.LocalLLMs (patterns for options, client, model download, NuGet)

## Deliverables Completed

### Documentation (docs/)
1. **getting-started.md** — Installation, quick start (5 min demo), model selection guide, configuration options, progress tracking, DI registration, troubleshooting
2. **api-reference.md** — Complete API docs for WhisperClient, WhisperOptions, TranscriptionResult, KnownWhisperModels, DI extension, error handling
3. **architecture.md** — High-level design, audio pipeline, model download flow, ONNX Runtime integration, caching strategy, memory usage, threading notes
4. **publishing.md** — GitHub Actions workflow, version management (semantic versioning), manual publishing steps, pre-release flow, checklist

### Root Documentation
- **README.md** — Follows ElBruno.LocalLLMs style with NuGet/build/license/HuggingFace/.NET/GitHub/Twitter badges, feature bullets, quick start code, model selection, available models table, progress tracking, DI example, building/contributing sections

### Sample Application
- **src/samples/HelloWhisper/Program.cs** — Robust console app with:
  - Argument validation (requires audio file path)
  - File existence check
  - Model selection display
  - Download progress reporting
  - Audio transcription with result display
  - Language detection output
  - Duration tracking
  - Comprehensive error handling (FileNotFoundException, InvalidOperationException, general exceptions)

### CI/CD Workflows
1. **.github/workflows/ci.yml** — Triggers on push/PR to main:
   - Setup .NET 8.0
   - Restore, build (Release mode), test
   - Uploads test results as artifacts
   - Runs on ubuntu-latest

2. **.github/workflows/publish.yml** — Triggers on GitHub release or workflow_dispatch:
   - Restore, build, test (Release mode)
   - Pack NuGet package
   - Push to NuGet.org using API key
   - Skip duplicate versions

### Issue #8 Community Support (2026-04-10)
- Analyzed user's "Connection refused" error after WAV upload
- Determined root cause: Environmental network issue, not library defect (model download blocked)
- Posted detailed troubleshooting comment with:
  - Network/firewall diagnostic steps
  - Pre-download and offline cache options
  - Clear explanation that library code is functioning correctly
- Closed issue as "not planned" appropriately
- Improved community documentation for future users with similar network issues

### Test Audio Files Documentation (2026-04-10, commit 1a5a9ee)
- Created comprehensive `testdata/audio/README.md` with:
  - Table of all 3 test audio files with sizes, durations, descriptions, model compatibility notes
  - Model-specific guidance (tiny.en behavior vs base/small/medium/large)
  - C# usage examples for unit and integration tests
  - Test coverage breakdown (AudioProcessorTests, WhisperTranscriptionTests)
  - Known issues and fixes (Issue #7: ONNX reshape error with zero-dimension cache tensors)
  - Attribution and licensing notes
  - References to related code and issues

- Created `docs/testing.md` testing guide with:
  - Test organization (unit, integration, samples)
  - How test data files are copied to build output via .csproj
  - Running tests with filter commands for unit vs integration
  - CI/CD pipeline documentation
  - Troubleshooting section (TestData not found, model download issues, memory issues)
  - Test templates for writing new unit and integration tests
  - Test statistics (218 total tests: ~200 unit, ~18 integration)

- Updated `README.md` with:
  - Links to new testing guide and test audio files documentation
  - "Testing" section explaining unit vs integration tests
  - Quick test command examples with appropriate filters
  - Reference to test audio files location

- All changes verified:
  - Build succeeds (Release configuration)
  - 109 unit tests pass (all filters applied)
  - Documentation links tested and valid
  - Test file copying works correctly via .csproj

- Design decision documented: Separate documentation files for audio assets (what/where/compatibility) and testing procedures (how to run/CI/CD) following ElBruno convention

## Learnings
