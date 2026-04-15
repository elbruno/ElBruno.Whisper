# Lambert — History

## Project Context
- **Project:** ElBruno.Whisper — .NET library for local Whisper speech-to-text
- **User:** Bruno Capuano
- **Stack:** .NET 8.0/10.0, C#, ONNX Runtime, ElBruno.HuggingFace.Downloader
- **Reference repo:** ElBruno.LocalLLMs (patterns for options, client, model download, NuGet)

## Learnings

### Test Project Creation
- **Date:** 2025-01-XX
- **Action:** Created comprehensive test project with 7 test files covering all core components
- **Details:**
  - Test project already had correct csproj with xUnit, Moq, and coverlet
  - Created WhisperOptionsTests.cs: 13 tests covering default values and configuration
  - Created WhisperModelDefinitionTests.cs: 6 tests for model properties and sizes
  - Created KnownWhisperModelsTests.cs: 11 tests validating all pre-defined models
  - Created TranscriptionResultTests.cs: 10 tests for transcription result properties
  - Created Audio/WavReaderTests.cs: 10 tests for WAV file parsing with in-memory test data
  - Created Audio/MelSpectrogramTests.cs: 9 tests for mel spectrogram computation
  - Created WhisperServiceExtensionsTests.cs: 10 tests for DI registration
- **Total:** 69 comprehensive unit tests using xUnit patterns
- **Test patterns:** Used [Fact] and [Theory], descriptive test names (MethodName_Condition_ExpectedResult), synthetic test data, no external file dependencies

### New Tests for Inference, AudioProcessor, and WhisperClient
- **Date:** 2025-06-XX
- **Action:** Added 4 new test files covering WhisperInferenceSession, AudioProcessor, WhisperClient, and integration tests
- **Details:**
  - Updated csproj to copy TestData/**/* to output directory (PreserveNewest)
  - Created Inference/WhisperInferenceSessionTests.cs: 4 tests — IDisposable check, constructor throws on missing files, sealed type check
  - Created Audio/AudioProcessorTests.cs: 10 tests — ProcessAudioFile/ProcessAudioStream with 3 real WAV files (Theory), finite values, file/stream match, invalid inputs, non-zero values
  - Created WhisperClientTests.cs: 8 tests — IDisposable, CreateAsync failure modes, sealed, API method existence via reflection
  - Created Integration/WhisperTranscriptionTests.cs: 6 tests — full transcription with model download, marked [Trait("Category", "Integration")] with 5-minute timeout
- **Total:** 32 new tests (109 total non-integration, up from 77 original)
- **Key discoveries:**
  - InternalsVisibleTo already configured in csproj for ElBruno.Whisper.Tests — can test AudioProcessor and WhisperInferenceSession directly
  - WhisperClient has private constructor, only CreateAsync factory — test failure modes by providing non-existent ModelPath/CacheDirectory
  - AudioProcessor.ProcessAudioFile always returns exactly 240,000 floats (80 mel bins × 3000 frames) regardless of input audio length
  - ProcessAudioFile and ProcessAudioStream produce identical output for the same WAV file
  - Test audio files: test-audio-failing.wav (353KB), test-audio-small.wav (205KB), test-audio-medium.wav (355KB) — all 16kHz mono PCM
  - Integration tests use [Trait("Category", "Integration")] and filter with --filter "Category!=Integration"

### 2026-03-30 - Cross-team sync: Ripley's tensor rank fix + finalized test coverage
- Ripley fixed Issue #3: ONNX tensor rank mismatch in WhisperInferenceSession
- All 109 tests passing with fix applied
- Test infrastructure ready for CI/CD pipeline
- Key decision documented: ONNX input tensor rank must match model declaration exactly

### 2026-04-15 - Timestamp support tests (Issue #12)
- **Action:** Wrote comprehensive tests for Ripley's timestamp implementation
- **New files created:**
  - `TranscriptionSegmentTests.cs`: 14 tests — properties, record equality, hash code, ToString, `with` expression, sealed check
  - `Tokenizer/WhisperTokenizerTimestampTests.cs`: 22 tests — IsTimestampToken boundary (50363=false, 50364=true), GetTimestamp known values (0.00s, 0.02s, 1.00s), DecodeWithTimestamps segment extraction, special token skipping, trailing text handling, empty segment skipping
  - `testdata/tokenizer/test-tokenizer.json`: Minimal tokenizer vocab for unit testing without real model
- **Existing files updated:**
  - `WhisperOptionsTests.cs`: +3 tests for EnableTimestamps (default false, set true, toggle back)
  - `TranscriptionResultTests.cs`: +6 tests for Segments property (default null, set list, empty list, IReadOnlyList type, timestamp data)
  - `ElBruno.Whisper.Tests.csproj`: Added tokenizer testdata content link
- **Total:** 171 tests passing (was 126), 45 new tests added
- **Edge cases discovered:**
  - DecodeWithTimestamps handles trailing text (start timestamp but no end) by emitting segment with start=end
  - Empty segments (two timestamps with no text between) are silently skipped (not emitted)
  - The WhisperTokenizer requires file-based construction — created a synthetic test-tokenizer.json with 8 vocab tokens + 6 special tokens for fast, isolated testing
  - Timestamp math: token 50364 + N → N * 0.02 seconds; token 50414 = exactly 1.00s (index 50)
