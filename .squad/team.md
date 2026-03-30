# Squad Team

> ElBruno.Whisper — .NET library for local Whisper speech-to-text with ONNX Runtime

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Dallas | Lead | `.squad/agents/dallas/charter.md` | 🏗️ Active |
| Ripley | Backend Dev | `.squad/agents/ripley/charter.md` | 🔧 Active |
| Lambert | Tester | `.squad/agents/lambert/charter.md` | 🧪 Active |
| Ash | DevRel | `.squad/agents/ash/charter.md` | 📝 Active |
| Scribe | Scribe | `.squad/agents/scribe/charter.md` | 📋 Active |
| Ralph | Work Monitor | — | 🔄 Monitor |

## Project Context

- **User:** Bruno Capuano
- **Project:** ElBruno.Whisper
- **Description:** .NET NuGet library for local Whisper speech-to-text. Auto-downloads ONNX models from HuggingFace on first use. Supports multiple model sizes (tiny, base, small, medium, large). Uses ElBruno.HuggingFace.Downloader for model downloads and ONNX Runtime for inference.
- **Tech Stack:** .NET 8.0/10.0, C#, ONNX Runtime, ElBruno.HuggingFace.Downloader
- **Reference:** ElBruno.LocalLLMs (patterns for model download, NuGet packaging, project structure)
- **Created:** 2026-03-30
