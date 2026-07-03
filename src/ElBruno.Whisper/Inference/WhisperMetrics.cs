using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ElBruno.Whisper.Inference;

internal static class WhisperMetrics
{
    internal const string MeterName = "ElBruno.Whisper";
    internal const string QueueWaitDurationMetricName = "elbruno.whisper.queue.wait.duration";
    internal const string InferenceDurationMetricName = "elbruno.whisper.inference.duration";

    private static readonly Meter s_meter = new(MeterName, "1.0.0");
    private static readonly Histogram<double> s_queueWaitDuration = s_meter.CreateHistogram<double>(
        QueueWaitDurationMetricName,
        unit: "ms",
        description: "How long transcription requests wait for an inference slot.");
    private static readonly Histogram<double> s_inferenceDuration = s_meter.CreateHistogram<double>(
        InferenceDurationMetricName,
        unit: "ms",
        description: "How long model inference takes once a request has acquired a session.");

    internal static void RecordQueueWait(
        string modelId,
        bool sessionPoolingEnabled,
        int maximumConcurrentRequests,
        TimeSpan duration,
        string outcome)
    {
        s_queueWaitDuration.Record(duration.TotalMilliseconds, CreateTags(
            modelId,
            sessionPoolingEnabled,
            maximumConcurrentRequests,
            outcome));
    }

    internal static void RecordInferenceDuration(
        string modelId,
        bool sessionPoolingEnabled,
        int maximumConcurrentRequests,
        TimeSpan duration,
        string outcome)
    {
        s_inferenceDuration.Record(duration.TotalMilliseconds, CreateTags(
            modelId,
            sessionPoolingEnabled,
            maximumConcurrentRequests,
            outcome));
    }

    private static TagList CreateTags(
        string modelId,
        bool sessionPoolingEnabled,
        int maximumConcurrentRequests,
        string outcome)
    {
        return new TagList
        {
            { "model.id", modelId },
            { "session.pooling", sessionPoolingEnabled },
            { "concurrency.limit", maximumConcurrentRequests },
            { "outcome", outcome }
        };
    }
}
