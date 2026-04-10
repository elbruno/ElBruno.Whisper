using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.Whisper.Inference;

/// <summary>
/// ONNX Runtime inference session for Whisper encoder-decoder pipeline.
/// Supports Optimum-style merged decoder with use_cache_branch and past key-value caching.
/// </summary>
internal sealed class WhisperInferenceSession : IDisposable
{
    private readonly InferenceSession _encoderSession;
    private readonly InferenceSession _decoderSession;
    private readonly int _numDecoderLayers;
    private readonly int _encoderDimension;
    private readonly bool _hasCacheBranch;
    private readonly List<CacheSlotInfo> _cacheSlots;
    private bool _disposed;

    /// <summary>
    /// Describes a past_key_value cache slot discovered from decoder model metadata.
    /// Maps an input name (past_key_values.*) to its corresponding output name (present.*).
    /// </summary>
    private readonly record struct CacheSlotInfo(
        string InputName,
        string OutputName,
        int[] MetadataShape);

    public WhisperInferenceSession(
        string encoderPath,
        string decoderPath,
        int numDecoderLayers = 4,
        int encoderDimension = 384)
    {
        _numDecoderLayers = numDecoderLayers;
        _encoderDimension = encoderDimension;

        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _encoderSession = new InferenceSession(encoderPath, options);
        _decoderSession = new InferenceSession(decoderPath, options);

        _hasCacheBranch = _decoderSession.InputMetadata.ContainsKey("use_cache_branch");
        _cacheSlots = DiscoverCacheSlots();
    }

    /// <summary>
    /// Discover past_key_values inputs from decoder model metadata at construction time.
    /// This makes the session work across all Whisper model sizes without hardcoding.
    /// </summary>
    private List<CacheSlotInfo> DiscoverCacheSlots()
    {
        var slots = new List<CacheSlotInfo>();

        foreach (var kvp in _decoderSession.InputMetadata)
        {
            if (!kvp.Key.StartsWith("past_key_values.", StringComparison.Ordinal))
                continue;

            // past_key_values.0.decoder.key → present.0.decoder.key
            var suffix = kvp.Key.Substring("past_key_values.".Length);
            var outputName = "present." + suffix;

            slots.Add(new CacheSlotInfo(kvp.Key, outputName, kvp.Value.Dimensions));
        }

        return slots;
    }

    /// <summary>
    /// Run inference on audio features.
    /// </summary>
    /// <param name="melSpectrogram">Log-mel spectrogram [1, 80, 3000]</param>
    /// <param name="initialTokens">Initial decoder tokens (e.g., special tokens)</param>
    /// <param name="maxTokens">Maximum tokens to generate</param>
    /// <param name="eotToken">End-of-text token ID</param>
    /// <returns>Generated token IDs</returns>
    public int[] Inference(float[] melSpectrogram, int[] initialTokens, int maxTokens, int eotToken)
    {
        var encoderHiddenStates = RunEncoder(melSpectrogram);
        var tokens = RunDecoder(encoderHiddenStates, initialTokens, maxTokens, eotToken);
        return tokens;
    }

    private float[] RunEncoder(float[] melSpectrogram)
    {
        var inputTensor = new DenseTensor<float>(melSpectrogram, new[] { 1, 80, 3000 });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_features", inputTensor)
        };

        using var results = _encoderSession.Run(inputs);
        return results.First().AsEnumerable<float>().ToArray();
    }

    private int[] RunDecoder(float[] encoderHiddenStates, int[] initialTokens, int maxTokens, int eotToken)
    {
        var tokens = new List<int>(initialTokens);

        var hiddenDim = encoderHiddenStates.Length / 1500;
        const int encoderSeqLen = 1500;

        var encoderTensor = new DenseTensor<float>(
            encoderHiddenStates,
            new[] { 1, encoderSeqLen, hiddenDim }
        );

        // KV cache: maps present.* output name → (data, shape) for feeding back as past_key_values.*
        Dictionary<string, (float[] Data, int[] Shape)>? kvCache = null;

        for (int step = 0; step < maxTokens; step++)
        {
            var isFirstStep = step == 0;
            // WORKAROUND: Always use cache branch to avoid ONNX model bug with empty encoder cache
            // The model has a Reshape bug when use_cache_branch=false with corrected mel spectrograms
            var useCacheBranch = true;

            // First step: full initial sequence. Cached steps: only the last generated token.
            long[] inputIds;
            int seqLen;
            if (isFirstStep)
            {
                inputIds = tokens.Select(t => (long)t).ToArray();
                seqLen = inputIds.Length;
            }
            else
            {
                inputIds = new[] { (long)tokens[tokens.Count - 1] };
                seqLen = 1;
            }

            var decoderInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids",
                    new DenseTensor<long>(inputIds, new[] { 1, seqLen })),
                NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoderTensor)
            };

            // Add use_cache_branch 1D bool tensor if the merged decoder expects it
            if (_hasCacheBranch)
            {
                decoderInputs.Add(NamedOnnxValue.CreateFromTensor("use_cache_branch",
                    new DenseTensor<bool>(new[] { useCacheBranch }, new[] { 1 })));
            }

            // Add past key-value cache inputs (empty on first step, cached on subsequent)
            AddCacheInputs(decoderInputs, kvCache);

            using var results = _decoderSession.Run(decoderInputs);

            // Extract logits and greedy-decode the last token position
            var logitsOutput = results.First(r => r.Name == "logits");
            var logits = logitsOutput.AsEnumerable<float>().ToArray();
            var vocabSize = logits.Length / seqLen;
            var lastTokenLogits = new float[vocabSize];
            Array.Copy(logits, (seqLen - 1) * vocabSize, lastTokenLogits, 0, vocabSize);

            var nextToken = ArgMax(lastTokenLogits);

            // Capture present.* outputs as cache for next step
            kvCache = ExtractPresentOutputs(results);

            if (nextToken == eotToken)
                break;

            tokens.Add(nextToken);
        }

        return tokens.ToArray();
    }

    /// <summary>
    /// Adds past key-value tensors to decoder inputs.
    /// First step (cache is null): provides zero-length tensors so the model graph is satisfied.
    /// Subsequent steps: feeds cached present values from the previous step.
    /// </summary>
    private void AddCacheInputs(
        List<NamedOnnxValue> inputs,
        Dictionary<string, (float[] Data, int[] Shape)>? cache)
    {
        foreach (var slot in _cacheSlots)
        {
            if (cache != null && cache.TryGetValue(slot.OutputName, out var cached))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(slot.InputName,
                    new DenseTensor<float>(cached.Data, cached.Shape)));
            }
            else
            {
                // Empty cache: create zero-filled tensors with all dynamic or zero dims=1
                // Fixes both ONNX dynamic dimensions (<0) and onnx-community model export bug (dims=0)
                // Without this, encoder cache tensors with shape [6,0,64] fail to reshape to [1,6,64,64]
                var shape = (int[])slot.MetadataShape.Clone();
                for (int d = 0; d < shape.Length; d++)
                {
                    if (shape[d] <= 0)
                        shape[d] = 1;
                }

                var totalElements = 1;
                foreach (var dim in shape)
                    totalElements *= dim;

                inputs.Add(NamedOnnxValue.CreateFromTensor(slot.InputName,
                    new DenseTensor<float>(new float[totalElements], shape)));
            }
        }
    }

    /// <summary>
    /// Extracts present.* key-value outputs from decoder results for caching.
    /// Copies tensor data so the results collection can be safely disposed.
    /// Ensures proper 4D shape for KV cache tensors.
    /// </summary>
    private static Dictionary<string, (float[] Data, int[] Shape)> ExtractPresentOutputs(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
    {
        var cache = new Dictionary<string, (float[] Data, int[] Shape)>();

        foreach (var result in results)
        {
            if (!result.Name.StartsWith("present.", StringComparison.Ordinal))
                continue;

            var tensor = result.AsTensor<float>();
            var dims = tensor.Dimensions.ToArray();
            var data = tensor.ToArray();
            
            // KV cache must be 4D: [batch, num_heads, seq_len, head_dim]
            // Fix common ONNX model output issues:
            // 1. Squeezed batch dimension (3D output) → prepend batch=1
            // 2. Zero batch dimension (encoder cache bug) → fix to batch=1 with proper data
            if (dims.Length == 3)
            {
                dims = new[] { 1 }.Concat(dims).ToArray();
            }
            else if (dims.Length == 4 && dims[0] == 0)
            {
                // Fix encoder cache batch=0 bug: model outputs [0,heads,seq,dim] with 0 elements
                // Change to [1,heads,seq,dim] and allocate proper zero-filled data array
                dims[0] = 1;
                var requiredElements = dims[0] * dims[1] * dims[2] * dims[3];
                if (data.Length == 0 && requiredElements > 0)
                {
                    data = new float[requiredElements]; // Zero-filled
                }
            }
            
            cache[result.Name] = (data, dims);
        }

        return cache;
    }

    private static int ArgMax(float[] values)
    {
        int maxIndex = 0;
        float maxValue = values[0];

        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > maxValue)
            {
                maxValue = values[i];
                maxIndex = i;
            }
        }

        return maxIndex;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _encoderSession?.Dispose();
            _decoderSession?.Dispose();
            _disposed = true;
        }
    }
}
