using System.Text;

using FlyingLogs.Core;

namespace FlyingLogs.Sinks;

public sealed class ConsoleSink : Sink
{
    private readonly Stream _consoleOut;
    private readonly ReadOnlyMemory<byte> _uNewLine = Encoding.UTF8.GetBytes(Environment.NewLine);

    public ConsoleSink() : base(LogEncodings.Utf8Plain)
    {
        Console.OutputEncoding = Encoding.UTF8;
        _consoleOut = Console.OpenStandardOutput();
    }

    public override void Ingest(
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

            // TODO: check if value is null. If yes, skip copy because we have nested fields instead.
            CopyToAndMoveDestination(propertyValues[printedProperties].Span, ref s);
            printedProperties++;

            if (depths.Length > printedProperties && depths[printedProperties] != 0)
            {
                // Complex property
                int lastDepth = 0;
                while (printedProperties < depths.Length && depths[printedProperties] != 0)
                {
                    int currentDepth = depths[printedProperties];
                    int depthDiff = currentDepth - lastDepth;
                    if (depthDiff == 0)
                        CopyToAndMoveDestination(","u8, ref s);
                    else
                    {
                        while (depthDiff > 0)
                        {
                            // Depth diff should never be greater than 1. Evaluate whether we can replace this with if.
                            CopyToAndMoveDestination("{"u8, ref s);
                            depthDiff--;
                        }

                        while (depthDiff < 0)
                        {
                            CopyToAndMoveDestination("}"u8, ref s);
                            depthDiff++;
                        }
                    }

                    if (currentDepth == 0)
                        break; // New property. This should be handled outside.

                    CopyToAndMoveDestination(names[printedProperties].Span, ref s);
                    CopyToAndMoveDestination(":"u8, ref s);
                    // TODO: check if value is null. If yes, skip copy because we have nested fields instead.
                    CopyToAndMoveDestination(propertyValues[printedProperties].Span, ref s);

                    printedProperties++;
                    lastDepth = currentDepth;
                }
            }
        }
        // Print last piece alone; no property.
        CopyToAndMoveDestination(template.MessagePieces.Span[template.MessagePieces.Length - 1].Span, ref s);

        for (int i = printedProperties; i < propertyValues.Count; i++)
        {
            if (i > 0)
            {
                // TODO duplicate code. refactor
                int depthDiff = depths[i] - depths[i - 1];
                if (depthDiff == 0)
                    CopyToAndMoveDestination(", "u8, ref s);
                else
                {
                    while (depthDiff > 0)
                    {
                        // Depth diff should never be greater than 1. Evaluate whether we can replace this with if.
                        CopyToAndMoveDestination("{"u8, ref s);
                        depthDiff--;
                    }

                    while (depthDiff < 0)
                    {
                        CopyToAndMoveDestination("}"u8, ref s);
                        depthDiff++;
                    }
                }
            }

            CopyToAndMoveDestination(names[i].Span, ref s);
            CopyToAndMoveDestination(":"u8, ref s);
            // TODO: check if value is null. If yes, skip copy because we have nested fields instead.
            CopyToAndMoveDestination(propertyValues[i].Span, ref s);
        }
        CopyToAndMoveDestination(_uNewLine.Span, ref s);

        int totalLength = tmpBuffer.Length - s.Length;
        _consoleOut.Write(tmpBuffer.Span.Slice(0, totalLength));
    }

    private static void CopyToAndMoveDestination(ReadOnlySpan<byte> source, ref Span<byte> destination)
    {
        source.CopyTo(destination);
        destination = destination.Slice(source.Length);
    }
}