namespace FlyingLogs.Core.Sinks;

public interface IClefSink : ISink
{
    void IngestClef(ReadOnlyMemory<byte> logEvent, Memory<byte> temporaryBuffer);
}