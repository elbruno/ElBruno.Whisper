# Decision: Merged Decoder KV Cache via Runtime Metadata Discovery

**Date:** 2025-07-14
**By:** Ripley (Backend Dev)
**Status:** Implemented
**Issue:** #1

## Context

The Optimum-style `decoder_model_merged.onnx` from `onnx-community/whisper-*` requires a `use_cache_branch` boolean input and past key-value cache management. Without these, the decoder fails at runtime.

## Decision

Discover past_key_values cache slots dynamically from `InferenceSession.InputMetadata` at construction time, rather than hardcoding layer counts or head dimensions. This makes the implementation work across all Whisper model sizes (tiny through large-v3) without any model-specific code paths.

### Key patterns:
- **Metadata discovery**: Filter `InputMetadata` for `past_key_values.*` keys, map to `present.*` output names
- **Zero tensors on first step**: Clone metadata shape, replace dynamic dims with batch=1, seq=0
- **Cache cycling**: `present.*` outputs → copy data/shape → feed as `past_key_values.*` inputs next step
- **Backward compatibility**: `_hasCacheBranch` flag gates the `use_cache_branch` input for non-merged models

## Consequences

- **Positive**: Works across all 10 known Whisper model sizes without changes. No hardcoded layer/head counts.
- **Positive**: Significant performance improvement — cached steps only pass 1 token instead of the full growing sequence.
- **Trade-off**: Each step copies present tensor data to float[] arrays. Could be optimized with OrtValue pinning in the future.
