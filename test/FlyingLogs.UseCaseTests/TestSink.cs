using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs.UseCaseTests
{
    /// <summary>
    /// A simple sink that moves all the functionality to provided delegates to allow customization for testing.
    /// </summary>
    internal class TestSink : ISink
    {
        private Func<LogEncoding> _expectedEncodingGetter;
        private Predicate<LogLevel> _isLogLevelActivePredicate;
        private Action<RawLog> _onIngest;

        public TestSink(
            Func<LogEncoding> expectedEncodingGetter,
            Predicate<LogLevel> isLogLevelActivePredicate,
            Action<RawLog> onIngest)
        {
            _expectedEncodingGetter = expectedEncodingGetter;
            _isLogLevelActivePredicate = isLogLevelActivePredicate;
            _onIngest = onIngest;
        }

        public LogEncoding ExpectedEncoding => _expectedEncodingGetter();

        public bool IsLogLevelActive(LogLevel level) => _isLogLevelActivePredicate(level);

        public void Ingest(RawLog log)
        {
            _onIngest(log);
        }
    }
}
