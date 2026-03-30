# Ripley — History

## Project Context
- **Project:** ElBruno.Whisper — .NET library for local Whisper speech-to-text
- **User:** Bruno Capuano
- **Stack:** .NET 8.0/10.0, C#, ONNX Runtime, ElBruno.HuggingFace.Downloader
- **Reference repo:** ElBruno.LocalLLMs (patterns for options, client, model download, NuGet)

## Learnings

## Learnings

### 2026-03-30 12:16 - Core Library Implementation
- Implemented complete ElBruno.Whisper library with 13 core files
- Created model definitions: WhisperModelSize enum, WhisperModelDefinition record, KnownWhisperModels with 10 pre-defined ONNX models from onnx-community
- Implemented pure C# audio processing: WavReader for 16-bit PCM WAV files, AudioProcessor for log-mel spectrogram generation (80 mel bins, 3000 frames = 30s)
- Built MelSpectrogramProcessor with STFT, Hanning window, mel filterbank, and Cooley-Tukey FFT implementation
- Created WhisperTokenizer for decoding model outputs with special token handling (startoftranscript, transcribe, translate, notimestamps)
- Implemented WhisperInferenceSession using Microsoft.ML.OnnxRuntime for encoder-decoder pipeline with autoregressive decoding
- Built WhisperClient with static CreateAsync factory pattern (matches LocalLLMs design)
- Integrated ElBruno.HuggingFace.Downloader for automatic model downloads to %LOCALAPPDATA%/ElBruno/Whisper/models
- Added DI extensions (WhisperServiceExtensions) for easy service registration
- Default model: WhisperTinyEn (~75MB) for fastest quick-start experience
- All implementations follow ElBruno repository conventions: net8.0;net10.0 multi-targeting, proper namespacing, comprehensive XML documentation
