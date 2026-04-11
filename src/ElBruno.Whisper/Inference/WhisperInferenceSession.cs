using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.Whisper.Inference;

/// <summary>
/// ONNX Runtime inference session for Whisper encoder-decoder pipeline.
/// Uses a non-merged decoder for the first step (computes fresh KV from encoder output)
/// and an Optimum-style merged decoder with KV caching for subsequent steps.
/// </summary>
internal sealed class WhisperInferenceSession : IDisposable
{
    private readonly InferenceSession _encoderSession;
    private readonly InferenceSession _firstStepDecoderSession;
    private readonly InferenceSession _cachedDecoderSession;
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

        // Load the non-merged decoder for the first step (no cache inputs required).
        // This cleanly avoids a Reshape bug in the merged model's conditional branch
        // that fails with empty/initial cache tensors.
        var decoderDir = Path.GetDirectoryName(decoderPath)!;
        var firstStepPath = Path.Combine(decoderDir, "decoder_model.onnx");
        if (File.Exists(firstStepPath))
        {
            _firstStepDecoderSession = new InferenceSession(firstStepPath, options);
        }
        else
        {
            // Fall back to merged decoder if non-merged is not available
            _firstStepDecoderSession = null!;
        }

        _cachedDecoderSession = new InferenceSession(decoderPath, options);

        _hasCacheBranch = _cachedDecoderSession.InputMetadata.ContainsKey("use_cache_branch");
        _cacheSlots = DiscoverCacheSlots();
    }

    /// <summary>
    /// Discover past_key_values inputs from cached decoder model metadata at construction time.
    /// This makes the session work across all Whisper model sizes without hardcoding.
    /// </summary>
    private List<CacheSlotInfo> DiscoverCacheSlots()
    {
        var slots = new List<CacheSlotInfo>();

        foreach (var kvp in _cachedDecoderSession.InputMetadata)
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
    public int[] Inference(float[] melSpectrogram, int[] initialTokens, int maxTokens, int eotToken,
        int[]? suppressTokens = null, int[]? beginSuppressTokens = null)
    {
        var encoderHiddenStates = RunEncoder(melSpectrogram);
        var tokens = RunDecoder(encoderHiddenStates, initialTokens, maxTokens, eotToken,
            suppressTokens, beginSuppressTokens);
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

    private int[] RunDecoder(float[] encoderHiddenStates, int[] initialTokens, int maxTokens, int eotToken,
        int[]? suppressTokens = null, int[]? beginSuppressTokens = null)
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

        // Whisper's positional embedding supports at most 448 total tokens.
        // Cap maxTokens to avoid exceeding the model's context window.
        const int maxModelPositions = 448;
        var maxGenerateSteps = Math.Min(maxTokens, maxModelPositions - initialTokens.Length);

        for (int step = 0; step < maxGenerateSteps; step++)
        {
            var isFirstStep = step == 0;

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

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;

            if (isFirstStep && _firstStepDecoderSession != null)
            {
                // First step: use the non-merged decoder which computes fresh KV
                // from encoder_hidden_states without requiring past_key_values inputs.
                results = RunFirstStepDecoder(inputIds, seqLen, encoderTensor);
            }
            else
            {
                // Subsequent steps (or fallback): use merged decoder with KV cache
                results = RunCachedDecoder(inputIds, seqLen, encoderTensor, kvCache, isFirstStep);
            }

            using (results)
            {
                // Extract logits and greedy-decode the last token position
                var logitsOutput = results.First(r => r.Name == "logits");
                var logits = logitsOutput.AsEnumerable<float>().ToArray();
                var vocabSize = logits.Length / seqLen;
                var lastTokenLogits = new float[vocabSize];
                Array.Copy(logits, (seqLen - 1) * vocabSize, lastTokenLogits, 0, vocabSize);

                // Suppress specified tokens (e.g., timestamp tokens when noTimestamps)
                if (suppressTokens != null)
                {
                    foreach (var t in suppressTokens)
                    {
                        if (t >= 0 && t < vocabSize)
                            lastTokenLogits[t] = float.NegativeInfinity;
                    }
                }

                // At the first generated position, apply begin_suppress_tokens.
                // This prevents the model from immediately outputting blank/EOT.
                if (isFirstStep && beginSuppressTokens != null)
                {
                    foreach (var t in beginSuppressTokens)
                    {
                        if (t >= 0 && t < vocabSize)
                            lastTokenLogits[t] = float.NegativeInfinity;
                    }
                }

                var nextToken = ArgMax(lastTokenLogits);

                // Update KV cache from present.* outputs. On the first step, all slots
                // are populated. On subsequent steps, only decoder self-attention KV is
                // updated (encoder cross-attention outputs are empty with batch=0 because
                // the cache branch passes them through unchanged).
                kvCache = ExtractPresentOutputs(results, kvCache);

                if (nextToken == eotToken)
                    break;

                tokens.Add(nextToken);
            }
        }

        return tokens.ToArray();
    }

    /// <summary>
    /// Runs the non-merged decoder for the first step. This model takes only input_ids and
    /// encoder_hidden_states, computes fresh decoder self-attention and encoder cross-attention
    /// KV values, and outputs them as present.* for caching in subsequent steps.
    /// </summary>
    private IDisposableReadOnlyCollection<DisposableNamedOnnxValue> RunFirstStepDecoder(
        long[] inputIds, int seqLen, DenseTensor<float> encoderTensor)
    {
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",
                new DenseTensor<long>(inputIds, new[] { 1, seqLen })),
            NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoderTensor)
        };

        return _firstStepDecoderSession.Run(inputs);
    }

    /// <summary>
    /// Runs the merged decoder with KV cache for subsequent steps (or as fallback for the first
    /// step when the non-merged decoder is not available).
    /// </summary>
    private IDisposableReadOnlyCollection<DisposableNamedOnnxValue> RunCachedDecoder(
        long[] inputIds, int seqLen, DenseTensor<float> encoderTensor,
        Dictionary<string, (float[] Data, int[] Shape)>? kvCache, bool isFirstStep)
    {
        // When falling back to merged decoder for first step, use_cache_branch must be true
        // with minimal cache tensors to avoid the model's Reshape bug with empty cache.
        var useCacheBranch = !isFirstStep || _firstStepDecoderSession == null;

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",
                new DenseTensor<long>(inputIds, new[] { 1, seqLen })),
            NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoderTensor)
        };

        if (_hasCacheBranch)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("use_cache_branch",
                new DenseTensor<bool>(new[] { useCacheBranch }, new[] { 1 })));
        }

        AddCacheInputs(inputs, kvCache);

        return _cachedDecoderSession.Run(inputs);
    }

    /// <summary>
    /// Adds past key-value tensors to decoder inputs.
    /// Uses cached values when available, or creates minimal dummy tensors for graph satisfaction.
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
                // Create minimal tensors with dim=1 for all dynamic/zero dims.
                // Required to satisfy the model's Reshape ops in the graph.
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
    /// Preserves existing cache entries when the model outputs empty tensors (batch=0),
    /// which happens for encoder cross-attention KV when use_cache_branch=true.
    /// </summary>
    private static Dictionary<string, (float[] Data, int[] Shape)> ExtractPresentOutputs(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results,
        Dictionary<string, (float[] Data, int[] Shape)>? existingCache)
    {
        var cache = existingCache != null
            ? new Dictionary<string, (float[] Data, int[] Shape)>(existingCache)
            : new Dictionary<string, (float[] Data, int[] Shape)>();

        foreach (var result in results)
        {
            if (!result.Name.StartsWith("present.", StringComparison.Ordinal))
                continue;

            var tensor = result.AsTensor<float>();
            var dims = tensor.Dimensions.ToArray();

            // Skip empty outputs (batch=0) — these are encoder KV pass-throughs
            // when use_cache_branch=true. Keep the existing cache entry instead.
            if (dims.Length >= 1 && dims[0] == 0)
                continue;

            var data = tensor.ToArray();
            
            // KV cache must be 4D: [batch, num_heads, seq_len, head_dim]
            if (dims.Length == 3)
            {
                dims = new[] { 1 }.Concat(dims).ToArray();
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
            _firstStepDecoderSession?.Dispose();
            _cachedDecoderSession?.Dispose();
            _disposed = true;
        }
    }
}
