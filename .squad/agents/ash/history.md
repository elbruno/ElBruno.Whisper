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

## Learnings
