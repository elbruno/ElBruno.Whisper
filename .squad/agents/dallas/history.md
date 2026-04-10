# Dallas — History

## Project Context
- **Project:** ElBruno.Whisper — .NET library for local Whisper speech-to-text
- **User:** Bruno Capuano
- **Stack:** .NET 8.0/10.0, C#, ONNX Runtime, ElBruno.HuggingFace.Downloader
- **Reference repo:** ElBruno.LocalLLMs (patterns for options, client, model download, NuGet)

## Learnings

### 2025-01-20: Project scaffold created
- **Architecture:** ElBruno.Whisper uses Microsoft.ML.OnnxRuntime (NOT OnnxRuntimeGenAI) because Whisper is an encoder-decoder model requiring separate encoder/decoder ONNX sessions. GenAI is for autoregressive LLMs only.
- **Model source:** Using onnx-community Whisper models from HuggingFace (whisper-tiny, whisper-base, whisper-small, whisper-medium, whisper-large-v3, whisper-large-v3-turbo). These come pre-exported with encoder_model.onnx and decoder_model_merged.onnx.
- **Project structure:**
  - Multi-target: net8.0 and net10.0 (LTS + latest)
  - Library: src/ElBruno.Whisper/
  - Tests: src/tests/ElBruno.Whisper.Tests/
  - Samples: src/samples/HelloWhisper/
  - Internal structure: Models/, Audio/, Inference/, Tokenizer/
- **Dependencies:** ElBruno.HuggingFace.Downloader (0.6.0), Microsoft.ML.OnnxRuntime (1.22.0), Microsoft.Extensions.DependencyInjection.Abstractions (9.0.0), Microsoft.Extensions.Logging.Abstractions (9.0.0)
- **Testing:** xUnit with Moq for mocking
- **Solution format:** .slnx (XML-based), not .sln
- **Key file paths:**
  - Root config: global.json, Directory.Build.props, LICENSE, .gitignore
  - Library: src/ElBruno.Whisper/ElBruno.Whisper.csproj
  - Tests: src/tests/ElBruno.Whisper.Tests/ElBruno.Whisper.Tests.csproj
  - Sample: src/samples/HelloWhisper/HelloWhisper.csproj
  - Images: images/nuget_logo.png (for NuGet package icon)

### 2026-04-10: Issue #7 Analysis and Triage
- Identified root cause: ONNX cache dimension normalization logic uses `shape[d] < 0` but onnx-community models export encoder cache tensors with explicit 0-length dimensions
- Invalid shapes like `{6,0,64}` cause reshape failures to 4D tensors `{1,6,64,64}` in decoder
- Pattern discovered: `ExtractPresentOutputs()` already handled zero-dimension batch sizes, extending to cache initialization achieves consistency
- Risk assessment: Low — dummy cache data (~36KB) overwritten on first step, backward-compatible with models without zero-dimension issue
- All 218 existing tests pass with 1-line fix
