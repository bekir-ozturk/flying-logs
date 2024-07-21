using System.Text;

using FlyingLogs.Core;
using FlyingLogs.Core.Sinks;
using FlyingLogs.Shared;

namespace FlyingLogs.Sinks;

public sealed class ConsoleSink : IStructuredUtf8PlainSink
{
    private readonly Stream _consoleOut;
    private readonly ReadOnlyMemory<byte> _uNewLine = Encoding.UTF8.GetBytes(Environment.NewLine);
    private LogLevel _minimumLevelOfInterest = Configuration.LogLevelNone;

    public LogLevel MinimumLevelOfInterest => _minimumLevelOfInterest;

    public ConsoleSink(LogLevel minimumLevelOfInterest)
    {
        _minimumLevelOfInterest = minimumLevelOfInterest;
        Console.OutputEncoding = Encoding.UTF8;
        _consoleOut = Console.OpenStandardOutput();
    }

    bool ISink.SetLogLevelForSink(ISink sink, LogLevel level)
    {
        bool anyChanges = false;
        if (sink == this)
        {
            anyChanges = _minimumLevelOfInterest != level;
            _minimumLevelOfInterest = level;
        }
        
        return anyChanges;
    }

    public void Ingest(
        LogTemplate template,
        IReadOnlyList<ReadOnlyMemory<byte>> propertyValues,
        Memory<byte> tmpBuffer)
    {
        /* Standard output stream is already synchronized; we can use it from multiple threads.
         * But we can't write to it piece by piece since other threads may write their pieces in between.
         * Move the whole message into a buffer and write at once to avoid this issue. */
        var s = tmpBuffer.Span;

        {
            DateTime.UtcNow.TryFormat(s, out int bytesWritten);
            s = s.Slice(bytesWritten);
            CopyToAndMoveDestination(" "u8, ref s);
        }

        CopyToAndMoveDestination(Constants.LogLevelsUtf8Plain.Span[(int)template.Level].Span, ref s);
        CopyToAndMoveDestination(" "u8, ref s);

        int printedProperties = 0;
        var depths = template.PropertyDepths.Span;
        var names = template.PropertyNames.Span;

        // Leave the last piece out; its special.
        for (int i = 0; i < template.MessagePieces.Length - 1; i++)
        {
            CopyToAndMoveDestination(template.MessagePieces.Span[i].Span, ref s);

            // Use plain values instead of json encoded. Console output doesn't need to be a valid JSON.
            var valueStr = ClefFormatter.PropertyValueToJsonString(
                printedProperties,
                template,
                propertyValues,
                s,
                out _,
                out int usedProperties);

            if (usedProperties < 1)
            {
                // Error processing value. Skip this and children.
                printedProperties++;
                while (printedProperties < propertyValues.Count && template.PropertyDepths.Span[printedProperties] != 0)
                    printedProperties++;
            }
            else
            {
                printedProperties += usedProperties;
            }

            CopyToAndMoveDestination(valueStr, ref s);
        }
        // Print last piece alone; no property.
        CopyToAndMoveDestination(template.MessagePieces.Span[template.MessagePieces.Length - 1].Span, ref s);

        // Additional properties
        for (int i = printedProperties; i < propertyValues.Count;)
        {
            CopyToAndMoveDestination(" \""u8, ref s);
            CopyToAndMoveDestination(template.PropertyNames.Span[i].Span, ref s);
            CopyToAndMoveDestination("\":"u8, ref s);

            // Use plain values instead of json encoded. Console output doesn't need to be a valid JSON.
            var valueStr = ClefFormatter.PropertyValueToJsonString(
                i,
                template,
                propertyValues,
                s,
                out _,
                out int usedProperties);

            if (usedProperties < 1)
            {
                // Error processing value. Skip this and children.
                i++;
                while (i < propertyValues.Count && template.PropertyDepths.Span[i] != 0)
                    i++;
            }
            else
            {
                i += usedProperties;
            }

            CopyToAndMoveDestination(valueStr, ref s);
        }

        CopyToAndMoveDestination(_uNewLine.Span, ref s);

        int totalLength = tmpBuffer.Length - s.Length;
        _consoleOut.Write(tmpBuffer.Span[..totalLength]);
    }

    private static void CopyToAndMoveDestination(ReadOnlySpan<byte> source, ref Span<byte> destination)
    {
        source.CopyTo(destination);
        destination = destination.Slice(source.Length);
    }
}