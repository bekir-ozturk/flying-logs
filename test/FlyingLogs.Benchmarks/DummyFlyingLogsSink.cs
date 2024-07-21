
using FlyingLogs.Core;
using FlyingLogs.Core.Sinks;
using FlyingLogs.Shared;

public class DummyFlyingLogsSink : IStructuredUtf8PlainSink
{
    private LogLevel _minimumLevelOfInterest = LogLevel.Trace;

    public LogLevel MinimumLevelOfInterest => _minimumLevelOfInterest;

    public void Ingest(LogTemplate template, IReadOnlyList<ReadOnlyMemory<byte>> propertyValues, Memory<byte> temporaryBuffer)
    {
        // Do nothing.
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