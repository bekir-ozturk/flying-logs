using System.Globalization;

using FlyingLogs.Shared;

namespace FlyingLogs.Core.Sinks;

public class ClefFormatter : IStructuredUtf8JsonSink
{
    private Config<IClefSink> _config;

    public ClefFormatter(params IClefSink[] sinks)
    {
        _config = new (sinks);
    }

    public LogLevel MinimumLevelOfInterest => throw new NotImplementedException();

    bool ISink.SetLogLevelForSink(ISink sink, LogLevel level)
    {
        return ISink.SetLogLevelForSink(ref _config, sink, level);
    }

    public void IngestJson(LogTemplate template, IReadOnlyList<ReadOnlyMemory<byte>> plainPropertyValues, IReadOnlyList<ReadOnlyMemory<byte>> jsonPropertyValues, Memory<byte> temporaryBuffer)
    {
        var clefBuffer = LogEventToClef(template, jsonPropertyValues, temporaryBuffer, out int usedBytes);

        if (clefBuffer.IsEmpty)
            return;

        Memory<byte> remainingBuffer = temporaryBuffer.Slice(usedBytes);
        foreach(var sink in _config.Sinks)
        {
            if (sink.MinimumLevelOfInterest <= template.Level)
            {
                sink.IngestClef(clefBuffer, remainingBuffer);
            }
        }
    }

    public static ReadOnlyMemory<byte> LogEventToClef(
        LogTemplate template,
        IReadOnlyList<ReadOnlyMemory<byte>> jsonPropertyValues,
        Memory<byte> temporaryBuffer,
        out int usedBytes)
    {
        Span<byte> writeHead = temporaryBuffer.Span;

        try
        {
            CopyToAndMoveDestination("{\""u8, ref writeHead);
            CopyToAndMoveDestination("@t\":\""u8, ref writeHead);
            // TODO process the returned value.
            _ = DateTime.UtcNow.TryFormat(writeHead, out int timestampBytes, "O", CultureInfo.InvariantCulture);
            writeHead = writeHead.Slice(timestampBytes);
            CopyToAndMoveDestination("\",\"@mt\":\""u8, ref writeHead);
            CopyToAndMoveDestination(template.TemplateString.Span, ref writeHead);
            CopyToAndMoveDestination("\",\"@i\":\""u8, ref writeHead);
            CopyToAndMoveDestination(template.EventId.Span, ref writeHead);
            CopyToAndMoveDestination("\",\"@l\":\""u8, ref writeHead);
            CopyToAndMoveDestination(Constants.LogLevelsUtf8Json.Span[(int)template.Level].Span, ref writeHead);
            CopyToAndMoveDestination("\""u8, ref writeHead);
            
            int propCount = template.PropertyNames.Length;
            int previousPropertyDepth = 0;
            for (int i = 0; i < propCount; i++)
            {
                int depthDiff = template.PropertyDepths.Span[i] - previousPropertyDepth;
                while (template.PropertyDepths.Span[i] > previousPropertyDepth)
                {
                    previousPropertyDepth++;
                    CopyToAndMoveDestination("{"u8, ref writeHead);
                }
                
                while (template.PropertyDepths.Span[i] < previousPropertyDepth)
                {
                    previousPropertyDepth--;
                    CopyToAndMoveDestination("}"u8, ref writeHead);
                }

                // Don't start with a comma if we just went to a deeper level to avoid {,"name":"value"}.
                CopyToAndMoveDestination(depthDiff > 0 ? "\""u8 : ",\""u8, ref writeHead);
                CopyToAndMoveDestination(template.PropertyNames.Span[i].Span, ref writeHead);
                CopyToAndMoveDestination("\":"u8, ref writeHead);

                ReadOnlySpan<byte> value = jsonPropertyValues[i].Span;
                if (value == PropertyValueHints.Complex.Span)
                {
                    if (i + 1 == propCount || template.PropertyDepths.Span[i+1] <= previousPropertyDepth)
                    {
                        // The object is complex, but has no fields.
                        CopyToAndMoveDestination("{}"u8, ref writeHead);
                    }
                    continue;
                }
                else if (value == PropertyValueHints.Null.Span || value == PropertyValueHints.ComplexNull.Span)
                {
                    CopyToAndMoveDestination("null"u8, ref writeHead);

                    // If this is a complex object with null value, we should skip the deeper fields.
                    while (i + 1 < propCount && template.PropertyDepths.Span[i+1] > previousPropertyDepth)
                        i++;
                    continue;
                }

                bool addQuotes = true;
                // If value length is zero, fallback to quotes to avoid invalid JSON.
                if (value.Length != 0)
                {
                    BasicPropertyType basicType = template.PropertyTypes.Span[i];
                    if (basicType == BasicPropertyType.Integer ||
                        (basicType == BasicPropertyType.Fraction &&
                        value[^1] != "y"u8[0] // Catch Infinity and -Infinity
                        && value[^1] != "N"u8[0])) // Catch NaN
                    {
                        addQuotes = false;
                    }
                }
                
                if (addQuotes)
                {
                    CopyToAndMoveDestination("\""u8, ref writeHead);
                }

                CopyToAndMoveDestination(value, ref writeHead);

                if (addQuotes)
                {
                    CopyToAndMoveDestination("\""u8, ref writeHead);
                }
            }

            // Close all open curly brackets. One extra for the main CLEF object.
            while (-1 < previousPropertyDepth--)
                CopyToAndMoveDestination("}"u8, ref writeHead);
        }
        catch (Exception e) when (e is IndexOutOfRangeException // Thrown by span[length]
            || e is ArgumentException // Thrown by span.CopyTo()
            || e is ArgumentOutOfRangeException) // Thrown by span.Slice()
        {
            Metrics.BufferTooSmall.Add(1);
            usedBytes = 0;
            return ReadOnlyMemory<byte>.Empty;
        }
        catch
        {
            // TODO Maybe we should throw our own exception and invoke caller's error handler here.
            // IndexOutOfRange exception can better be thrown as InsufficientBufferSpaceException.
            // But can we be sure that IOOR isn't thrown in any other case?
            // We don't want to falsely blame buffer size.
            usedBytes = 0;
            return ReadOnlyMemory<byte>.Empty;
        }

        usedBytes = temporaryBuffer.Length - writeHead.Length;
        return temporaryBuffer[..usedBytes];
    }

    private static void CopyToAndMoveDestination(ReadOnlySpan<byte> source, ref Span<byte> destination)
    {
        source.CopyTo(destination);
        destination = destination.Slice(source.Length);
    }
}