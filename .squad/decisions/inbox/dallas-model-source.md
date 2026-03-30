### 2025-01-20T16:00:01Z: Architecture decision — Model source
**By:** Dallas (Lead)
**What:** Using onnx-community Whisper models from HuggingFace (e.g., onnx-community/whisper-tiny, whisper-base, whisper-small, whisper-medium, whisper-large-v3, whisper-large-v3-turbo). These repos contain pre-exported ONNX models with encoder, decoder, and all required config/tokenizer files.
**Why:** Avoids needing to convert models ourselves. The onnx-community repos are maintained and widely used.
