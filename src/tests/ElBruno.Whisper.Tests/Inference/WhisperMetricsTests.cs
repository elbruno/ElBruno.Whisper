using System.Collections.Generic;
using System.Diagnostics.Metrics;
using ElBruno.Whisper.Inference;
using Xunit;

namespace ElBruno.Whisper.Tests.Inference;

public class WhisperMetricsTests
{
    [Fact]
    public void QueueWaitMetric_EmitsMeasurements()
    {
        var measurements = new List<double>();
        using var listener = CreateListener(WhisperMetrics.QueueWaitDurationMetricName, measurements);

        WhisperMetrics.RecordQueueWait(
            "whisper-tiny.en",
            sessionPoolingEnabled: true,
            maximumConcurrentRequests: 2,
            duration: TimeSpan.FromMilliseconds(12),
            outcome: "acquired");

        Assert.Single(measurements);
        Assert.Equal(12, measurements[0], precision: 6);
    }

    [Fact]
    public void InferenceDurationMetric_EmitsMeasurements()
    {
        var measurements = new List<double>();
        using var listener = CreateListener(WhisperMetrics.InferenceDurationMetricName, measurements);

        WhisperMetrics.RecordInferenceDuration(
            "whisper-tiny.en",
            sessionPoolingEnabled: false,
            maximumConcurrentRequests: 1,
            duration: TimeSpan.FromMilliseconds(25),
            outcome: "success");

        Assert.Single(measurements);
        Assert.Equal(25, measurements[0], precision: 6);
    }

    private static MeterListener CreateListener(string instrumentName, ICollection<double> measurements)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == WhisperMetrics.MeterName && instrument.Name == instrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
        {
            if (instrument.Name == instrumentName)
            {
                measurements.Add(value);
            }
        });

        listener.Start();
        return listener;
    }
}
