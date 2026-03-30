# Decision: ONNX tensor inputs must match expected rank exactly

**By:** Ripley (Backend Dev)
**Date:** 2025-07-14
**Issue:** #3

## Context
The `use_cache_branch` input for the merged decoder was created as a scalar (rank 0) tensor using `new int[0]` as the shape. ONNX Runtime strictly validates tensor rank and the model declares this input as rank 1 (shape [1]).

## Decision
Always create ONNX tensor inputs with the exact rank the model expects. For single-element 1D inputs, use `new[] { 1 }` as the shape — never `new int[0]`.

## Consequence
This is a general rule for all future ONNX input construction in the inference pipeline.
