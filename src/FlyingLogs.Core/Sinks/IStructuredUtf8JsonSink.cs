namespace FlyingLogs.Core.Sinks;

public interface IStructuredUtf8JsonSink : ISink
{
    void IngestJson(
        LogTemplate template,
        IReadOnlyList<ReadOnlyMemory<byte>> plainPropertyValues,
        IReadOnlyList<ReadOnlyMemory<byte>> jsonPropertyValues,
        Memory<byte> temporaryBuffer);
}