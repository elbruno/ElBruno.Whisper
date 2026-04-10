# ElBruno.Whisper

[![NuGet](https://img.shields.io/nuget/v/ElBruno.Whisper.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Whisper)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ElBruno.Whisper.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Whisper)
[![Build Status](https://github.com/elbruno/ElBruno.Whisper/actions/workflows/ci.yml/badge.svg)](https://github.com/elbruno/ElBruno.Whisper/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![HuggingFace](https://img.shields.io/badge/🤗_HuggingFace-ONNX_Models-orange?style=flat-square)](https://huggingface.co/elbruno)
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%2010.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![GitHub stars](https://img.shields.io/github/stars/elbruno/ElBruno.Whisper?style=social)](https://github.com/elbruno/ElBruno.Whisper)
[![Twitter Follow](https://img.shields.io/twitter/follow/elbruno?style=social)](https://twitter.com/elbruno)

## Run local Whisper speech-to-text in .NET 🎤

Transcribe audio to text in .NET using OpenAI's Whisper model. Powered by ONNX Runtime with automatic model download from HuggingFace.

## Features

- 📦 **Automatic model download** — models are fetched from HuggingFace on first use
- 🔊 **Multiple model sizes** — tiny → base → small → medium → large (pick your speed/accuracy tradeoff)
- 🚀 **Zero friction** — works out of the box with sensible defaults (tiny.en)
- 🌍 **Multilingual support** — transcribe 99+ languages with multilingual models
- 💉 **DI-friendly** — register with `AddWhisper()` in ASP.NET Core
- 📊 **Progress reporting** — track model downloads with real-time callbacks
- 🎯 **English-optimized models** — dedicated `.en` variants for best accuracy on English audio

## Installation

```bash
dotnet add package ElBruno.Whisper
```

## Quick Start

```csharp
using ElBruno.Whisper;

// Create client (downloads tiny.en model on first run)
using var client = await WhisperClient.CreateAsync();

var result = await client.TranscribeAsync("audio.wav");
Console.WriteLine(result.Text);
```

## First Run

The first time you create a `WhisperClient`, the model is downloaded from HuggingFace to your local cache directory (~75 MB - 3 GB depending on model size). This typically takes **10-60 seconds** depending on your internet connection and chosen model.

**Track download progress:**
```csharp
using var client = await WhisperClient.CreateAsync(
    progress: new Progress<ElBruno.HuggingFace.DownloadProgress>(p =>
    {
        if (p.Stage == ElBruno.HuggingFace.DownloadStage.Downloading)
            Console.WriteLine($"{p.CurrentFile}: {p.PercentComplete:F0}%");
        else
            Console.WriteLine($"{p.Stage}: {p.Message}");
    })
);
```

**Subsequent runs load instantly** from cache (`%LOCALAPPDATA%/ElBruno/Whisper/models`).

## Model Selection

Whisper offers various model sizes. English-optimized models (`.en` suffix) are smaller and faster for English audio:

```csharp
using var client = await WhisperClient.CreateAsync(new WhisperOptions
{
    Model = KnownWhisperModels.WhisperSmallEn
});

var result = await client.TranscribeAsync("english-audio.wav");
Console.WriteLine(result.Text);
```

## Available Models

| Size | English | Multilingual | Parameters | Approx Size | Speed |
|------|---------|--------------|-----------|-------------|-------|
| tiny | tiny.en | tiny | 39M | 75 MB | ⚡⚡⚡⚡⚡ |
| base | base.en | base | 74M | 140 MB | ⚡⚡⚡⚡ |
| small | small.en | small | 244M | 460 MB | ⚡⚡⚡ |
| medium | medium.en | medium | 769M | 1.5 GB | ⚡⚡ |
| large | — | large | 1550M | 3.0 GB | ⚡ |

**Use English-optimized (`.en`) models for:**
- English audio only (slightly smaller, faster, better accuracy on English)

**Use Multilingual models for:**
- Non-English audio
- Mixed-language content
- Language auto-detection

## Progress Tracking

Monitor both file downloads and transcription progress:

```csharp
var downloadProgress = new Progress<ElBruno.HuggingFace.DownloadProgress>(p =>
{
    if (p.Stage == ElBruno.HuggingFace.DownloadStage.Downloading)
        Console.Write($"\r⬇️ {p.PercentComplete:F0}%");
    else
        Console.WriteLine($"\n✓ {p.Message}");
});

using var client = await WhisperClient.CreateAsync(progress: downloadProgress);

var result = await client.TranscribeAsync("audio.wav");
Console.WriteLine($"✓ Transcribed: {result.Text}");
```

## Dependency Injection

Register Whisper in ASP.NET Core or other DI-enabled applications:

```csharp
builder.Services.AddWhisper(options =>
{
    options.Model = KnownWhisperModels.WhisperBaseEn;
});

// Inject WhisperClient anywhere
public class TranscriptionService(WhisperClient whisper) { ... }
```

## Transcription Result

The `TranscriptionResult` includes:

```csharp
var result = await client.TranscribeAsync("audio.wav");

Console.WriteLine(result.Text);                    // Transcribed text
Console.WriteLine(result.DetectedLanguage);       // Detected language (for multilingual models)
Console.WriteLine(result.Duration);               // Audio duration
```

## Troubleshooting

**Model download fails?**
- Check your internet connection
- For private HuggingFace models, set the `HF_TOKEN` environment variable

**Out of memory?**
- Use a smaller model (tiny or base instead of medium/large)
- Transcribe shorter audio files in chunks

For detailed troubleshooting, see [docs](docs/).

## Samples

| Sample | Description |
|--------|-------------|
| [HelloWhisper](src/samples/HelloWhisper) | Minimal console transcription |
| [BlazorWhisper](src/samples/BlazorWhisper) | Blazor app with audio recording and real-time transcription |

## Documentation

- [Getting Started](docs/getting-started.md) — installation, first steps, configuration
- [API Reference](docs/api-reference.md) — full API documentation
- [Architecture](docs/architecture.md) — design decisions and internal structure
- [Testing Guide](docs/testing.md) — running tests, test organization, CI/CD pipeline
- [Test Audio Files](testdata/audio/README.md) — audio resources for testing and transcription validation
- [Image Prompts](docs/image-prompts.md) — prompts for generating blog and social media images
- [Publishing](docs/publishing.md) — NuGet package publishing with OIDC

## Building from Source

```bash
git clone https://github.com/elbruno/ElBruno.Whisper
cd ElBruno.Whisper
dotnet build ElBruno.Whisper.slnx
dotnet test ElBruno.Whisper.slnx --filter "Category!=Integration"
```

## Testing

The repository includes comprehensive unit and integration tests:

**Quick test run (unit tests, no model download):**
```bash
dotnet test ElBruno.Whisper.slnx --filter "Category!=Integration"
```

**Full test run (includes integration with real models):**
```bash
dotnet test ElBruno.Whisper.slnx
```

Test audio files are provided in [`testdata/audio/`](testdata/audio/) for validation and transcription testing. For details, see the [Testing Guide](docs/testing.md).

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

## Related Projects

- [ElBruno.LocalLLMs](https://github.com/elbruno/ElBruno.LocalLLMs) — Run local LLMs in .NET
- [ElBruno.HuggingFace](https://github.com/elbruno/ElBruno.HuggingFace) — HuggingFace model utilities for .NET

## 🙏 Acknowledgments

- [ONNX Runtime](https://github.com/microsoft/onnxruntime) — inference engine
- [OpenAI Whisper](https://github.com/openai/whisper) — speech-to-text model
- [Hugging Face](https://huggingface.co/) — model hosting and community
- [ONNX Community](https://huggingface.co/onnx-community) — ONNX model conversions

## 👋 About the Author

**Made with ❤️ by [Bruno Capuano (ElBruno)](https://github.com/elbruno)**

- 📝 **Blog**: [elbruno.com](https://elbruno.com)
- 📺 **YouTube**: [youtube.com/elbruno](https://youtube.com/elbruno)
- 🔗 **LinkedIn**: [linkedin.com/in/elbruno](https://linkedin.com/in/elbruno)
- 𝕏 **Twitter**: [twitter.com/elbruno](https://twitter.com/elbruno)
- 🎙️ **Podcast**: [notienenombre.com](https://notienenombre.com)
