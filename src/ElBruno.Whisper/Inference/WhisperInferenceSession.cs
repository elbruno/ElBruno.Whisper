using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.Whisper.Inference;

/// <summary>
/// ONNX Runtime inference session for Whisper encoder-decoder pipeline.
/// </summary>
internal sealed class WhisperInferenceSession : IDisposable
{
    private readonly InferenceSession _encoderSession;
    private readonly InferenceSession _decoderSession;
    private bool _disposed;

    public WhisperInferenceSession(string encoderPath, string decoderPath)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _encoderSession = new InferenceSession(encoderPath, options);
        _decoderSession = new InferenceSession(decoderPath, options);
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
        // 1. Run encoder
        var encoderHiddenStates = RunEncoder(melSpectrogram);

        // 2. Run decoder autoregressively
        var tokens = RunDecoder(encoderHiddenStates, initialTokens, maxTokens, eotToken);

        return tokens;
    }

    private float[] RunEncoder(float[] melSpectrogram)
    {
        // Input: input_features [1, 80, 3000]
        var inputTensor = new DenseTensor<float>(melSpectrogram, new[] { 1, 80, 3000 });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_features", inputTensor)
        };

        using var results = _encoderSession.Run(inputs);
        var output = results.First().AsEnumerable<float>().ToArray();
        
        return output;
    }

    private int[] RunDecoder(float[] encoderHiddenStates, int[] initialTokens, int maxTokens, int eotToken)
    {
        var tokens = new List<int>(initialTokens);
        
        // Determine encoder hidden states shape from encoder output
        // Typically [1, seq_len, hidden_dim] where seq_len is 1500 for 30s audio
        // For whisper-tiny: hidden_dim = 384, seq_len = 1500
        var hiddenDim = encoderHiddenStates.Length / 1500; // Assumes seq_len = 1500
        var seqLen = encoderHiddenStates.Length / hiddenDim;

        for (int i = 0; i < maxTokens; i++)
        {
            // Prepare decoder inputs
            var inputIds = tokens.ToArray();
            var inputIdsTensor = new DenseTensor<long>(
                inputIds.Select(t => (long)t).ToArray(),
                new[] { 1, inputIds.Length }
            );

            var encoderHiddenStatesTensor = new DenseTensor<float>(
                encoderHiddenStates,
                new[] { 1, seqLen, hiddenDim }
            );

            var decoderInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoderHiddenStatesTensor)
            };

            // Run decoder
            using var results = _decoderSession.Run(decoderInputs);
            
            // Get logits (last token)
            var logits = results.First().AsEnumerable<float>().ToArray();
            
            // Greedy decode: argmax of last token
            var lastTokenLogits = new float[logits.Length / inputIds.Length];
            var offset = (inputIds.Length - 1) * lastTokenLogits.Length;
            Array.Copy(logits, offset, lastTokenLogits, 0, lastTokenLogits.Length);
            
            var nextToken = ArgMax(lastTokenLogits);
            
            // Check for end of sequence
            if (nextToken == eotToken)
                break;
            
            tokens.Add(nextToken);
        }

        return tokens.ToArray();
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
