# Testing Guide

This guide explains how to run tests and understand the test structure for ElBruno.Whisper.

## Test Organization

Tests are organized into three categories:

### 1. Unit Tests (Fast, No Dependencies)

**Location:** `src/tests/ElBruno.Whisper.Tests/`

These tests verify individual components without downloading models or accessing external resources.

**Coverage:**
- `Audio/` — WAV parsing, audio processing, mel spectrogram generation
- `Inference/` — ONNX inference session, cache management, token handling
- `WhisperClientTests.cs` — Client initialization and error handling
- Model definitions, options validation, tokenization

**Run unit tests only:**
```bash
dotnet test ElBruno.Whisper.slnx --filter "Category!=Integration"
```

**Expected runtime:** < 30 seconds on modern hardware

### 2. Integration Tests (Slow, Model Download Required)

**Location:** `src/tests/ElBruno.Whisper.Tests/Integration/`

These tests download real Whisper models from HuggingFace and perform end-to-end transcription. They validate the complete pipeline: model download → audio loading → mel spectrogram → ONNX inference → token decoding → text output.

**Coverage:**
- Real model loading and caching
- Complete audio-to-text transcription
- Language detection
- Duration calculation
- Error handling with actual models

**Run integration tests only:**
```bash
dotnet test ElBruno.Whisper.slnx --filter "Category=Integration"
```

**Expected runtime:** 5-15 minutes (first run with model download), <1 minute with cached model

**Timeout:** 5 minutes per test

**CI/CD:** Typically excluded with `--filter "Category!=Integration"` to keep CI fast

### 3. Sample Applications

**Location:** `src/samples/`

Sample console and Blazor applications demonstrating library usage.

**Run samples:**
```bash
# Console sample with audio file transcription
dotnet run -p src/samples/HelloWhisper -- <path-to-audio.wav>

# Blazor sample (interactive web UI)
cd src/samples/BlazorWhisper
dotnet run
```

## Test Data

All tests use audio files from `testdata/audio/`:

| File | Purpose | Notes |
|------|---------|-------|
| **test-audio-small.wav** (201 KB) | Basic smoke tests | Works with all models |
| **test-audio-medium.wav** (347 KB) | Fuller transcription tests | Tiny model may return empty text |
| **test-audio-failing.wav** (345 KB) | ONNX edge case testing | Previously crashed, now validates fix |

For detailed information about test audio files, see [`testdata/audio/README.md`](../../testdata/audio/README.md).

### How Test Files Are Copied

The test project (`.csproj`) automatically copies audio files to the test output directory:

```xml
<ItemGroup>
  <Content Include="..\..\..\testdata\audio\*">
    <Link>TestData\%(Filename)%(Extension)</Link>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

Tests access them via:
```csharp
private static string GetTestDataPath(string fileName)
{
    return Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
}
```

## Running Tests

### All Tests
```bash
dotnet test ElBruno.Whisper.slnx
```

### Quick Build Check (No Tests)
```bash
dotnet build ElBruno.Whisper.slnx
```

### Specific Test Class
```bash
dotnet test ElBruno.Whisper.slnx --filter "AudioProcessorTests"
```

### Specific Test Method
```bash
dotnet test ElBruno.Whisper.slnx --filter "ProcessAudioFile_ReturnsExpectedLength"
```

### Verbose Output
```bash
dotnet test ElBruno.Whisper.slnx -v detailed
```

### With Coverage (requires coverlet)
```bash
dotnet test ElBruno.Whisper.slnx /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## CI/CD Pipeline

### Continuous Integration Workflow

The repository uses GitHub Actions (`.github/workflows/ci.yml`) to:

1. **Trigger:** On push to `main` or pull request
2. **Setup:** .NET 8.0 and .NET 10.0 SDKs
3. **Build:** `dotnet build ElBruno.Whisper.slnx --configuration Release`
4. **Test (Unit):** `dotnet test --filter "Category!=Integration"` — Fast verification
5. **Artifacts:** Test results uploaded for analysis

### Publishing Workflow

The repository uses GitHub Actions (`.github/workflows/publish.yml`) to:

1. **Trigger:** On GitHub Release or manual workflow dispatch
2. **Build & Test:** Full release build with unit tests only
3. **Pack:** NuGet package creation
4. **Publish:** Push to NuGet.org with OIDC authentication

## Troubleshooting

### Test Failures

**AudioProcessorTests fail with "TestData not found"**
- Ensure files are copied: Check `bin/Debug/net8.0/TestData/` directory
- Rebuild the test project: `dotnet clean && dotnet build`

**Integration tests timeout or fail to download model**
- Check internet connection and HuggingFace availability
- Manually pre-download with: `HF_TOKEN=your-token dotnet test` (if using private models)
- Skip integration tests: Use `--filter "Category!=Integration"`

**"Out of memory" on large models**
- Use a smaller model (tiny or base) for testing
- Reduce concurrent test execution: `dotnet test -p:ParallelizeTestCollections=false`

**Model download fails with authentication error**
- For private HuggingFace models, set environment variable:
  ```bash
  set HF_TOKEN=your_huggingface_token
  dotnet test
  ```

## Writing New Tests

### Unit Test Template

```csharp
using Xunit;
using ElBruno.Whisper;

namespace ElBruno.Whisper.Tests;

public class MyFeatureTests
{
    [Fact]
    public void FeatureWorksCorrectly()
    {
        // Arrange
        var feature = new MyFeature();
        
        // Act
        var result = feature.DoSomething();
        
        // Assert
        Assert.NotNull(result);
    }
    
    [Theory]
    [InlineData("test-audio-small.wav")]
    [InlineData("test-audio-medium.wav")]
    public void FeatureWorksWithMultipleAudios(string audioFile)
    {
        // Use test data files
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", audioFile);
        var result = feature.ProcessAudio(path);
        Assert.NotNull(result);
    }
}
```

### Integration Test Template

```csharp
using Xunit;

namespace ElBruno.Whisper.Tests.Integration;

[Trait("Category", "Integration")]
public class MyIntegrationTests
{
    private const int TimeoutMs = 5 * 60 * 1000; // 5 minutes
    
    [Fact(Timeout = TimeoutMs)]
    public async Task TranscriptionWorks()
    {
        var options = new WhisperOptions
        {
            Model = KnownWhisperModels.WhisperTinyEn
        };
        
        using var client = await WhisperClient.CreateAsync(options);
        
        var audioPath = Path.Combine(AppContext.BaseDirectory, "TestData", "test-audio-small.wav");
        var result = await client.TranscribeAsync(audioPath);
        
        Assert.NotNull(result.Text);
    }
}
```

## Test Statistics

As of the latest version:

- **Total tests:** 218 (varies with recent changes)
- **Unit tests:** ~200
- **Integration tests:** ~18
- **Code coverage:** Core library coverage via integration tests
- **xUnit framework:** v2.9.3
- **Test SDK:** v17.12.0

## References

- [xUnit Documentation](https://xunit.net/)
- [Trait-based test filtering](https://xunit.net/docs/getting-started/netcore)
- [GitHub Actions CI/CD docs](.github/workflows/)
- [Test Audio Files](../../testdata/audio/README.md)
