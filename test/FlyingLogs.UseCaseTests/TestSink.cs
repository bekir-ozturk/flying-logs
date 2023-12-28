using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs.UseCaseTests
{
    /// <summary>
    /// A simple sink that moves all the functionality to provided delegates to allow customization for testing.
    /// </summary>
    internal class TestSink : ISink
    {
        public Func<LogEncodings> ExpectedEncodingGetter { get; set; }
        public Action<RawLog> OnIngest { get; set; }

        public TestSink()
        {
            SetDelegates();
        }

        public LogEncodings ExpectedEncoding => ExpectedEncodingGetter();

        public void Ingest(RawLog log)
        {
            OnIngest(log);
        }

        public void SetDelegates(
            Func<LogEncodings>? expectedEncodingGetter = default,
            Action<RawLog>? onIngest = default)
        {
            ExpectedEncodingGetter = expectedEncodingGetter ?? (() => LogEncodings.Utf8Plain);
            OnIngest = onIngest ?? (l => { });
        }
    }
}
