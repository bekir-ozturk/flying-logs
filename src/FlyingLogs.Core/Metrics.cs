using System.Diagnostics.Metrics;

namespace FlyingLogs.Shared
{
    public static class Metrics
    {
        private static readonly Meter LoggingMeter = new Meter("FlyingLogs", "1.0.0");

        public static Counter<int> QueueOverflow = LoggingMeter.CreateCounter<int>("queue-overflow");
        public static Counter<int> HttpResponseReceived = LoggingMeter.CreateCounter<int>("http-response");
        public static Counter<int> IngestedEvents = LoggingMeter.CreateCounter<int>("ingested-events");
        public static Histogram<int> IngestionTimeMs = LoggingMeter.CreateHistogram<int>("ingestion-time-ms");
    }
}
