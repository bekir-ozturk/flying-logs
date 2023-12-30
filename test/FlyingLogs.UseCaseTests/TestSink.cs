using FlyingLogs.Core;

namespace FlyingLogs.UseCaseTests
{
    /// <summary>
    /// A simple sink that moves all the functionality to provided delegates to allow customization for testing.
    /// </summary>
    internal class TestSink : Sink
    {
        public Action<RawLog> OnIngest { get; set; } = (l) => { };

        public TestSink(LogEncodings encoding) : base(encoding) { }

        public override void Ingest(RawLog log)
        {
            OnIngest(log);
        }
    }
}
