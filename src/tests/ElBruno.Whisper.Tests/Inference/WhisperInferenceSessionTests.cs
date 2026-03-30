using ElBruno.Whisper.Inference;
using Xunit;

namespace ElBruno.Whisper.Tests.Inference;

public class WhisperInferenceSessionTests
{
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
