using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs.UseCaseTests
{
    /// <summary>
    /// A simple sink that moves all the functionality to provided delegates to allow customization for testing.
    /// </summary>
    internal class TestSink : IStructuredUtf8PlainSink
    {
        private LogLevel _minimumLevelOfInterest = LogLevel.Trace;

        public LogLevel MinimumLevelOfInterest => _minimumLevelOfInterest;

        public Action<LogTemplate, IReadOnlyList<ReadOnlyMemory<byte>>> OnIngest { get; set; }
            = (template, plainValues) => { };

        public void Ingest(
            LogTemplate template,
            IReadOnlyList<ReadOnlyMemory<byte>> propertyValues,
            Memory<byte> temporaryBuffer)
        {
            OnIngest(template, propertyValues);
        }

        bool ISink.SetLogLevelForSink(ISink sink, LogLevel level) 
        {
            if (sink == this)
            {
                _minimumLevelOfInterest = level;
                return true;
            }
            return false;
        }
    }
}
