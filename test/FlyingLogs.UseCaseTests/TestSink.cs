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

        public TestSink()
        {
            SetDelegates();
        }

        public LogEncoding ExpectedEncoding => _expectedEncodingGetter();

        public bool IsLogLevelActive(LogLevel level) => _isLogLevelActivePredicate(level);

        public void Ingest(RawLog log)
        {
            _onIngest(log);
        }

        public void SetDelegates(
            Func<LogEncoding>? expectedEncodingGetter = default,
            Predicate<LogLevel>? isLogLevelActivePredicate = default,
            Action<RawLog>? onIngest = default)
        {
            _expectedEncodingGetter = expectedEncodingGetter ?? (() => LogEncoding.Utf8Plain);
            _isLogLevelActivePredicate = isLogLevelActivePredicate ?? (l => true);
            _onIngest = onIngest ?? (l => { });
        }
    }
}
