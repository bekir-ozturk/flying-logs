using System.Diagnostics.Metrics;

namespace FlyingLogs.Shared
{
    internal static class Metrics
    {
        private static readonly Meter LoggingMeter = new Meter("FlyingLogs", "1.0.0");

        internal static Counter<int> QueueOverflow = LoggingMeter.CreateCounter<int>("queue-overflow");
        internal static Counter<int> HttpResponseReceived = LoggingMeter.CreateCounter<int>("http-response");
        internal static Histogram<int> IngestionTimeMs = LoggingMeter.CreateHistogram<int>("ingestion-time-ms");
    }
}
