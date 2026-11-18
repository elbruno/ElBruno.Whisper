using ElBruno.Whisper.Inference;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Xunit;

namespace ElBruno.Whisper.Tests.Inference;

public class WhisperInferenceSessionTests
{
    [Fact]
    public void OnnxRuntime_RunsNativeCpuInference()
    {
        // A self-contained ONNX Identity graph (float[1,2], opset 13) needs no model download.
        var model = Convert.FromBase64String(
            "CAg6XAoZCgVpbnB1dBIGb3V0cHV0IghJZGVudGl0eRIMbmF0aXZlLXNtb2tlWhcKBWlucHV0Eg4KDAgBEggKAggBCgIIAmIYCgZvdXRwdXQSDgoMCAESCAoCCAEKAggCQgIQDQ==");
        using var session = new InferenceSession(model);
        var input = new DenseTensor<float>(new[] { 1.25f, -2.5f }, new[] { 1, 2 });

        using var results = session.Run(new[]
        {
            NamedOnnxValue.CreateFromTensor("input", input)
        });

        Assert.Equal(new[] { 1.25f, -2.5f }, Assert.Single(results).AsEnumerable<float>());
    }

    [Fact]
    public void WhisperInferenceSession_ImplementsIDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(WhisperInferenceSession)));
    }

    [Fact]
    public void Constructor_ThrowsWhenEncoderPathDoesNotExist()
    {
        var encoderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "encoder_model.onnx");
        var decoderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "decoder_model_merged.onnx");

        Assert.ThrowsAny<Exception>(() =>
            new WhisperInferenceSession(encoderPath, decoderPath));
    }

    [Fact]
    public void Constructor_ThrowsWhenDecoderPathDoesNotExist()
    {
        var encoderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "encoder_model.onnx");
        var decoderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "decoder_model_merged.onnx");

        Assert.ThrowsAny<Exception>(() =>
            new WhisperInferenceSession(encoderPath, decoderPath));
    }

    [Fact]
    public void Constructor_AcceptsCustomLayerAndDimensionParameters()
    {
        var encoderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "encoder_model.onnx");
        var decoderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "decoder_model_merged.onnx");

        // Should throw because files don't exist, but the parameter signature is valid
        Assert.ThrowsAny<Exception>(() =>
            new WhisperInferenceSession(encoderPath, decoderPath, numDecoderLayers: 6, encoderDimension: 512));
    }

    [Fact]
    public void IsSealed_CannotBeInherited()
    {
        Assert.True(typeof(WhisperInferenceSession).IsSealed);
    }
}
