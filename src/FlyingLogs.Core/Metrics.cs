using System.Diagnostics.Metrics;

namespace FlyingLogs.Core
{
    public static class Metrics
    {
        public static readonly Meter LoggingMeter = new Meter("FlyingLogs", "1.0.0");

        public static readonly Counter<int> SerializationError = LoggingMeter.CreateCounter<int>("flyinglogs-serialization-error");
        public static readonly Counter<int> UnsupportedRuntimeEncoding = LoggingMeter.CreateCounter<int>("flyinglogs-unsupported-runtime-encoding");
        public static readonly Counter<int> QueueOverflow = LoggingMeter.CreateCounter<int>("flyinglogs-queue-overflow");
        public static readonly Counter<int> HttpResponseReceived = LoggingMeter.CreateCounter<int>("flyinglogs-http-response");
        public static readonly Counter<int> IngestedEvents = LoggingMeter.CreateCounter<int>("flyinglogs-ingested-events");
        public static readonly Histogram<int> IngestionTimeMs = LoggingMeter.CreateHistogram<int>("flyinglogs-ingestion-time-ms");
    }
}
