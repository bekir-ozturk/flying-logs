using System.Diagnostics.Metrics;

namespace FlyingLogs.Core
{
    public static class Metrics
    {
        public static readonly Meter LoggingMeter = new Meter("FlyingLogs", "1.0.0");

        public static readonly Counter<int> SerializationError = LoggingMeter.CreateCounter<int>("flyinglogs-serialization-error");
        public static readonly Counter<int> BufferTooSmall = LoggingMeter.CreateCounter<int>("flyinglogs-buffer-too-small");
        // TODO Queues only exist in sinks. We should include which sink's queue overflowed. 
        public static readonly Counter<int> QueueOverflow = LoggingMeter.CreateCounter<int>("flyinglogs-queue-overflow");
        public static readonly Counter<int> HttpResponseReceived = LoggingMeter.CreateCounter<int>("flyinglogs-http-response");
        public static readonly Counter<int> IngestedEvents = LoggingMeter.CreateCounter<int>("flyinglogs-ingested-events");
        public static readonly Histogram<int> IngestionTimeMs = LoggingMeter.CreateHistogram<int>("flyinglogs-ingestion-time-ms");
    }
}
