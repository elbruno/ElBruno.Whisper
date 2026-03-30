### 2025-01-20T16:00:00Z: Architecture decision — ONNX Runtime choice
**By:** Dallas (Lead)
**What:** Using Microsoft.ML.OnnxRuntime (not OnnxRuntimeGenAI) for Whisper inference because Whisper is an encoder-decoder model requiring separate encoder/decoder ONNX sessions. GenAI is designed for autoregressive LLMs. Using onnx-community Whisper ONNX models from HuggingFace which come pre-exported with encoder_model.onnx and decoder_model_merged.onnx.
**Why:** Correct technical choice for Whisper's architecture. GenAI would not work.
