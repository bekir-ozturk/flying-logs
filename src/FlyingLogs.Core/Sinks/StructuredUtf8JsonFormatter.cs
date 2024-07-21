using FlyingLogs.Shared;

namespace FlyingLogs.Core.Sinks;

public class StructuredUtf8JsonFormatter : IStructuredUtf8PlainSink
{
    // Preallocated list that we use to store property values we JSON encoded before we pass it to the next sinks.
    private readonly ThreadLocal<List<ReadOnlyMemory<byte>>> _jsonPropertyValues
        = new(() => new List<ReadOnlyMemory<byte>>(16), false);

    private Config<IStructuredUtf8JsonSink> _config;

    public LogLevel MinimumLevelOfInterest => _config.MinimumLevelOfInterest;

    public StructuredUtf8JsonFormatter(params IStructuredUtf8JsonSink[] sinks)
    {
        _config = new(sinks);
    }

    bool ISink.SetLogLevelForSink(ISink sink, LogLevel level)
    {
        return ISink.SetLogLevelForSink(ref _config, sink, level);
    }

    public void Ingest(
        LogTemplate template,
        IReadOnlyList<ReadOnlyMemory<byte>> propertyValues,
        Memory<byte> temporaryBuffer)
    {
        int tmpBufferOffset = 0;
        List<ReadOnlyMemory<byte>> targetPropertyList = _jsonPropertyValues.Value!;

        // TODO handle returned errors.
        bool failed = Shared.JsonUtilities.JsonEncodePropertyValues(
            propertyValues,
            template.PropertyTypes,
            temporaryBuffer,
            ref tmpBufferOffset,
            targetPropertyList);

        Memory<byte> remainingBuffer = temporaryBuffer.Slice(tmpBufferOffset);

        foreach (var sink in _config.Sinks)
        {
            if (sink.MinimumLevelOfInterest <= template.Level)
            {
                sink.IngestJson(template, propertyValues, targetPropertyList, remainingBuffer);
            }
        }
    }
}