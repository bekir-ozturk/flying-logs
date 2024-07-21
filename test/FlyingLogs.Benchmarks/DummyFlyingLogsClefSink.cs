
using FlyingLogs.Core;
using FlyingLogs.Core.Sinks;
using FlyingLogs.Shared;

public class DummyFlyingLogsClefSink : IClefSink
{
    private LogLevel _minimumLevelOfInterest;
    private readonly byte[] _buffer = new byte[8 * 1024];

    public LogLevel MinimumLevelOfInterest => _minimumLevelOfInterest;

    public DummyFlyingLogsClefSink() : this(LogLevel.Trace) { }

    public DummyFlyingLogsClefSink(LogLevel minimumLevelOfInterest)
    {
        _minimumLevelOfInterest = minimumLevelOfInterest;
    }

    public void IngestClef(ReadOnlyMemory<byte> logEvent, Memory<byte> temporaryBuffer)
    {
        // Move data to its destination: _buffer
        logEvent.CopyTo(_buffer);
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