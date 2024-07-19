namespace FlyingLogs.Core;

public interface IStructuredUtf8PlainSink : ISink
{
    void Ingest(LogTemplate template, IReadOnlyList<ReadOnlyMemory<byte>> propertyValues, Memory<byte> temporaryBuffer);
}