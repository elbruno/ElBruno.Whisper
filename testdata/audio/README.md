# Test Audio Files

This directory contains WAV audio files used for testing the ElBruno.Whisper library and speech-to-text transcription functionality. These resources are shared across the test suite and can be reused by other libraries and applications.

## Overview

The test audio files cover different scenarios and audio characteristics to ensure comprehensive testing of transcription features. Each file is optimized for specific test cases and model compatibility testing.

## Test Audio Files

| File | Size | Duration | Description | Model Compatibility |
|------|------|----------|-------------|-------------------|
| **test-audio-small.wav** | 201 KB | ~2-3 sec | Short audio clip for basic transcription smoke tests. Contains recognizable English speech. | ✅ Works with tiny, base, small, medium, large models |
| **test-audio-medium.wav** | 347 KB | ~4-5 sec | Medium-length audio clip for fuller transcription tests. Contains longer English speech passage. | ⚠️ Tiny model may return empty text; base, small, medium, large models produce better results |
| **test-audio-failing.wav** | 345 KB | ~4-5 sec | Audio that previously exposed an ONNX reshape bug (Issue #7). Models with zero-dimension cache tensors would crash during decoder initialization. Now transcribes successfully after fix. | ✅ Works with all models (post Issue #7 fix) |
| **test-audio-48khz-stereo.wav** | 582 KB | ~3-4 sec | 48kHz stereo audio requiring resampling to 16kHz mono. Regression test for Issue #10 (empty inference output). Expected: contains "technology" and "AI". | ✅ Works with all models (post Issue #10 fix) |
| **test-audio-16khz-mono.wav** | 194 KB | ~3-4 sec | 16kHz mono audio in native Whisper format. Regression test for Issue #10 (empty inference output). Expected: contains "technology" and "AI". | ✅ Works with all models (post Issue #10 fix) |

## Model Notes

### Whisper Tiny.en (Default)
- **Pros:** Smallest (~75 MB), fastest inference, instant startup
- **Cons:** Lower accuracy, may skip audio or return empty text for some files
- **Best for:** Quick smoke tests, low-resource environments

### Whisper Base.en and Larger
- **Pros:** Better accuracy, handles more audio variations
- **Cons:** Larger models (140 MB - 3 GB), slower inference
- **Recommendation:** Use for integration tests requiring high-quality transcriptions

## Usage in C#

### Loading Audio Files for Testing

```csharp
// In test code, files are copied to TestData/ by the csproj
private static string GetTestDataPath(string fileName)
{
    return Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
}

// Unit test example
[Theory]
[InlineData("test-audio-small.wav")]
[InlineData("test-audio-medium.wav")]
[InlineData("test-audio-failing.wav")]
[InlineData("test-audio-48khz-stereo.wav")]
[InlineData("test-audio-16khz-mono.wav")]
public void AudioProcessor_HandlesTestFiles(string fileName)
{
    var processor = new AudioProcessor();
    var path = GetTestDataPath(fileName);
    
    var result = processor.ProcessAudioFile(path);
    
    Assert.NotNull(result);
    Assert.NotEmpty(result);
}
```

### Integration Testing with Transcription

```csharp
[Fact(Timeout = 5 * 60 * 1000)] // 5-minute timeout
[Trait("Category", "Integration")]
public async Task TranscribeSmallAudio_WithBaseModel()
{
    var options = new WhisperOptions
    {
        Model = KnownWhisperModels.WhisperBaseEn,
        Language = "en"
    };
    
    using var client = await WhisperClient.CreateAsync(options);
    
    var audioPath = GetTestDataPath("test-audio-small.wav");
    var result = await client.TranscribeAsync(audioPath);
    
    Assert.NotNull(result.Text);
    Assert.NotEmpty(result.Text);
}
```

## Running Tests

### Unit Tests Only (No Model Download)
```bash
dotnet test ElBruno.Whisper.slnx --filter "Category!=Integration"
```

### Integration Tests (Includes Transcription)
```bash
dotnet test ElBruno.Whisper.slnx --filter "Category=Integration"
```

### All Tests
```bash
dotnet test ElBruno.Whisper.slnx
```

## Test Coverage

The test audio files are used by:

1. **AudioProcessorTests** — Validates mel spectrogram generation
   - Tests file-based and stream-based audio loading
   - Verifies output shape [1, 80, 3000] (80 mel bins × 3000 time steps = 30 sec)
   - Ensures finite values (no NaN or infinity)
   - Confirms consistency between file and stream processing

2. **WhisperTranscriptionTests** — Integration tests with real models
   - Tests transcription accuracy and error handling
   - Verifies language detection
   - Checks duration calculation
   - Validates both file and stream transcription paths

## Known Issues and Fixes

### Issue #10: Inference Returns Empty Text (Fixed)

**Problem:** Whisper ONNX inference with `whisper-tiny.en` produced empty transcription output for all audio files.

**Root causes:** (1) Mel spectrogram used wrong Hann window, HTK mel scale instead of Slaney, and no STFT centering. (2) Merged ONNX decoder has an internal Reshape bug with empty cache tensors. (3) Tokenizer failed to load special tokens from `added_tokens` in `tokenizer.json`. (4) Missing `begin_suppress_tokens` logic allowed model to immediately output EOT.

**Fix:** Rewrote mel spectrogram computation, implemented dual-decoder architecture, fixed tokenizer loading, and added config-driven token suppression.

**Result:** All test audio files (including 48kHz stereo and 16kHz mono samples) transcribe correctly.

### Issue #7: ONNX Reshape Error (Fixed)

**Problem:** The `test-audio-failing.wav` file previously caused an ONNX Runtime reshape error when using onnx-community Whisper models. The models exported encoder cross-attention cache tensors with a 0-length dimension, which crashed the decoder initialization.

**Fix:** Updated `WhisperInferenceSession.AddCacheInputs()` to treat zero-dimension shapes the same as dynamic dimensions (`-1`). This allows successful tensor creation and reshape operations.

**Result:** All test audio files now transcribe successfully with all Whisper model sizes.

## Attribution and Licensing

These test audio files are project test data created specifically for ElBruno.Whisper testing and validation. They are provided under the MIT License, same as the main project.

If you need to reuse these files in other projects:
- **Personal use:** No restrictions
- **Commercial use:** Comply with MIT License terms
- **Distribution:** Include attribution to ElBruno.Whisper

## References

- **Main Project:** [ElBruno.Whisper](../../../README.md)
- **Audio Processing:** [AudioProcessor](../../src/ElBruno.Whisper/Audio/AudioProcessor.cs)
- **Inference:** [WhisperInferenceSession](../../src/ElBruno.Whisper/Inference/WhisperInferenceSession.cs)
- **Related Issues:**
  - [#7 — ONNX reshape error with zero-dimension cache](https://github.com/elbruno/ElBruno.Whisper/issues/7)
  - [#9 — Fix for zero-dimension tensor handling](https://github.com/elbruno/ElBruno.Whisper/pull/9)
  - [#10 — Inference returns empty text](https://github.com/elbruno/ElBruno.Whisper/issues/10)
